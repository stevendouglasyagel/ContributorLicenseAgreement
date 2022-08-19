/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.GitHubLinkClient;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.Aad;
    using Microsoft.Extensions.Logging;
    using PullRequest = GitOps.Abstractions.PullRequest;

    internal class PullRequestHandler : IAppEventHandler
    {
        private readonly AppState appState;
        private readonly IAadRequestClient aadRequestClient;
        private readonly IGitHubLinkRestClient gitHubLinkClient;
        private readonly GitHubHelper gitHubHelper;
        private readonly ILogger<CLA> logger;

        public PullRequestHandler(
            AppState appState,
            IAadRequestClient aadRequestClient,
            IGitHubLinkRestClient gitHubLinkClient,
            GitHubHelper gitHubHelper,
            ILogger<CLA> logger)
        {
            this.appState = appState;
            this.aadRequestClient = aadRequestClient;
            this.gitHubLinkClient = gitHubLinkClient;
            this.gitHubHelper = gitHubHelper;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Pull_Request;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (parameters.Length == 0)
            {
                logger.LogInformation("No primitive available");
                return appOutput;
            }

            var primitivesData = (IEnumerable<ClaPrimitive>)parameters[0];
            if (!primitivesData.Any())
            {
                return appOutput;
            }

            if (gitOpsPayload.PlatformContext.ActionType == PlatformEventActions.Synchronize)
            {
                logger.LogInformation("Not acting on synchronize action");
                return appOutput;
            }

            if (gitOpsPayload.PlatformContext.ActionType == PlatformEventActions.Closed)
            {
                appOutput.States = await CleanUpChecks(gitOpsPayload);
                logger.LogInformation("Checks cleaned up");
                return appOutput;
            }

            var primitive = primitivesData.First();

            if (NeedsLicense(primitive, gitOpsPayload.PullRequest))
            {
                logger.LogInformation("License needed for {Sender}", gitOpsPayload.PullRequest.User);

                var hasCla = await HasSignedClaAsync(appOutput, gitOpsPayload, primitive.AutoSignMsftEmployee);

                appOutput.Comment = await gitHubHelper.GenerateClaCommentAsync(primitive, gitOpsPayload, hasCla, gitOpsPayload.PullRequest.Sender);

                await gitHubHelper.CreateCheckAsync(gitOpsPayload, hasCla, long.Parse(gitOpsPayload.PullRequest.RepositoryId), gitOpsPayload.PullRequest.Sha);

                appOutput.States ??= new States
                {
                    StateCollection = new System.Collections.Generic.Dictionary<string, object>()
                };

                appOutput.States.StateCollection.Add(
                    $"{Constants.Check}-{gitOpsPayload.PullRequest.User}", await AddCheckToStatesAsync(gitOpsPayload));
            }
            else
            {
                logger.LogInformation("No license needed for {Sender}", gitOpsPayload.PullRequest.User);
                await gitHubHelper.CreateCheckAsync(gitOpsPayload, true, long.Parse(gitOpsPayload.PullRequest.RepositoryId), gitOpsPayload.PullRequest.Sha);
            }

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
        }

        private static bool NeedsLicense(ClaPrimitive primitive, PullRequest pullRequest)
        {
            return !primitive.BypassUsers.Contains(pullRequest.Sender)
                   && !primitive.BypassOrgs.Contains(pullRequest.OrganizationName)
                   && pullRequest.Files.Sum(f => f.Changes) >= primitive.MinimalChangeRequired.CodeLines
                   && pullRequest.Files.Count >= primitive.MinimalChangeRequired.Files;
        }

        private async Task<bool> HasSignedClaAsync(AppOutput appOutput, GitOpsPayload gitOpsPayload, bool autoSignMsftEmployee)
        {
            var gitHubUser = gitOpsPayload.PullRequest.User;

            var cla = await appState.ReadState<ContributorLicenseAgreement.Core.Handlers.Model.SignedCla>(gitHubUser);

            if (cla == null)
            {
                if (!autoSignMsftEmployee)
                {
                    return false;
                }

                var gitHubLink = await gitHubLinkClient.GetLink(gitHubUser);
                if (gitHubLink.GitHub == null)
                {
                    return false;
                }

                var aadUser = await aadRequestClient.ResolveUserAsync(gitHubLink.Aad.UserPrincipalName);
                if (!aadUser.WasResolved)
                {
                    return false;
                }

                cla = gitHubHelper.CreateCla(true, gitHubUser, appOutput, "Microsoft", msftMail: gitHubLink.Aad.UserPrincipalName);
            }

            if (!cla.Employee)
            {
                var timestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return cla.Expires == null || cla.Expires > timestamp;
            }
            else
            {
                var user = await aadRequestClient.ResolveUserAsync(cla.MsftMail);
                return user.WasResolved;
            }
        }

        private async Task<States> CleanUpChecks(GitOpsPayload payload)
        {
            var key = $"{Constants.Check}-{payload.PullRequest.User}";
            var checks = await appState.ReadState<List<string>>(key);
            if (checks == null)
            {
                return null;
            }

            return new States
            {
                StateCollection = new System.Collections.Generic.Dictionary<string, object>
                {
                    {
                        key,
                        checks.Where(s => !s.Equals(payload.PullRequest.Sha)).ToList()
                    }
                }
            };
        }

        private async Task<List<(long, string)>> AddCheckToStatesAsync(GitOpsPayload payload)
        {
            var key = $"{Constants.Check}-{payload.PullRequest.User}";

            var checks = await appState.ReadState<List<(long, string)>>(key);

            checks = checks ?? new List<(long, string)>();

            checks.Add((long.Parse(payload.PullRequest.RepositoryId), payload.PullRequest.Sha));

            return checks;
        }
    }
}
