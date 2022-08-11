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
    using GitOps.Clients.GitHub;
    using Microsoft.Extensions.Logging;
    using PullRequest = GitOps.Abstractions.PullRequest;

    internal class PullRequestHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly AppState appState;
        private readonly IAadRequestClient aadRequestClient;
        private readonly GitHubLinkRestClient gitHubLinkClient;
        private readonly GitHubHelper gitHubHelper;
        private readonly ILogger<CLA> logger;

        public PullRequestHandler(
            IGitHubClientAdapterFactory factory,
            AppState appState,
            IAadRequestClient aadRequestClient,
            GitHubLinkRestClient gitHubLinkClient,
            GitHubHelper gitHubHelper,
            ILogger<CLA> logger)
        {
            this.factory = factory;
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

            var primitive = primitivesData.First();

            if (NeedsLicense(primitive, gitOpsPayload.PullRequest))
            {
                // var hasCla = await HasSignedCla(appOutput, "JohannesLampel");
                var hasCla = await HasSignedClaAsync(appOutput, gitOpsPayload);

                appOutput.Comment = await gitHubHelper.GenerateCommentAsync(primitive, gitOpsPayload, hasCla, gitOpsPayload.PullRequest.Sender);

                await gitHubHelper.CreateCheckAsync(gitOpsPayload, hasCla, gitOpsPayload.PullRequest.Sha);

                appOutput.States ??= new States
                {
                    StateCollection = new System.Collections.Generic.Dictionary<string, object>()
                };

                appOutput.States.StateCollection.Add(
                    $"{Constants.Check}-{gitOpsPayload.PullRequest.User}", await AddCheckToStatesAsync(gitOpsPayload));
            }
            else
            {
                await gitHubHelper.CreateCheckAsync(gitOpsPayload, true, gitOpsPayload.PullRequest.Sha);
            }

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
        }

        private static bool NeedsLicense(ClaPrimitive primitive, PullRequest pullRequest)
        {
            return !primitive.SkipUsers.Contains(pullRequest.Sender)
                   && !primitive.SkipOrgs.Contains(pullRequest.OrganizationName)
                   && pullRequest.Files.Sum(f => f.Changes) >= primitive.MinimalChangeRequired.CodeLines
                   && pullRequest.Files.Count >= primitive.MinimalChangeRequired.Files;
        }

        private async Task<bool> HasSignedClaAsync(AppOutput appOutput, GitOpsPayload gitOpsPayload)
        {
            var gitHubUser = gitOpsPayload.PullRequest.User;

            var cla = await appState.ReadState<ContributorLicenseAgreement.Core.Handlers.Model.SignedCla>(gitHubUser);

            if (cla == null)
            {
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

                cla = gitHubHelper.CreateCla(true, gitHubUser, appOutput, msftMail: aadUser.PrincipalName);
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

        private async Task<List<string>> AddCheckToStatesAsync(GitOpsPayload payload)
        {
            var key = $"{Constants.Check}-{payload.PullRequest.User}";

            var checks = await appState.ReadState<List<string>>(key);

            checks = checks ?? new List<string>();

            checks.Add(payload.PullRequest.Sha);

            return checks;
        }
    }
}
