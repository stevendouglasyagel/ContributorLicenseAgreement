/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System;
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
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.Logging;
    using Octokit;
    using PullRequest = GitOps.Abstractions.PullRequest;

    internal class PullRequestHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly AppState appState;
        private readonly IAadRequestClient aadRequestClient;
        private readonly GitHubLinkRestClient gitHubLinkClient;
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly GitHubHelper gitHubHelper;
        private readonly ILogger<CLA> logger;

        public PullRequestHandler(
            IGitHubClientAdapterFactory factory,
            AppState appState,
            IAadRequestClient aadRequestClient,
            GitHubLinkRestClient gitHubLinkClient,
            PlatformAppFlavorSettings flavorSettings,
            GitHubHelper gitHubHelper,
            ILogger<CLA> logger)
        {
            this.factory = factory;
            this.appState = appState;
            this.aadRequestClient = aadRequestClient;
            this.gitHubLinkClient = gitHubLinkClient;
            this.flavorSettings = flavorSettings;
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

            // ToDo: what happens if we have conflicting primitives?
            var primitive = primitivesData.First();

            if (NeedsLicense(primitive, gitOpsPayload.PullRequest))
            {
                // var hasCla = await HasSignedCla(appOutput, "JohannesLampel");
                var hasCla = await HasSignedClaAsync(appOutput, gitOpsPayload);

                appOutput.Comment = await gitHubHelper.GenerateCommentAsync(primitive, gitOpsPayload, hasCla);

                var check = await CreateCheckAsync(gitOpsPayload, hasCla);

                if (!hasCla)
                {
                    appOutput.States ??= new States
                    {
                        StateCollection = new System.Collections.Generic.Dictionary<string, object>()
                    };

                    appOutput.States.StateCollection.Add(
                            $"{Constants.Check}-{gitOpsPayload.PullRequest.User}", await AddCheckToStatesAsync(check, gitOpsPayload));
                }
            }
            else
            {
                await CreateCheckAsync(gitOpsPayload, true);
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

        private async Task<CheckRun> CreateCheckAsync(GitOpsPayload gitOpsPayload, bool hasCla)
        {
            var client = await factory.GetGitHubClientAdapterAsync(
                gitOpsPayload.PlatformContext.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            var check = new NewCheckRun(Constants.CheckName, gitOpsPayload.PullRequest.Sha)
            {
                Status = hasCla ? CheckStatus.Completed : CheckStatus.InProgress,
                Output = new NewCheckRunOutput(Constants.CheckTitle, Constants.CheckSummary)
            };

            if (hasCla)
            {
                check.Conclusion = Enum.Parse<CheckConclusion>(Conclusion.Success.ToString(), true);
            }

            return await client.CreateCheckRunAsync(
                long.Parse(gitOpsPayload.PullRequest.RepositoryId), check);
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

                cla = gitHubHelper.CreateCla(true, gitHubUser, appOutput, aadUser.PrincipalName);

                await gitHubHelper.UpdateChecksAsync(gitOpsPayload, gitHubUser);
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

        private async Task<List<long>> AddCheckToStatesAsync(CheckRun check, GitOpsPayload payload)
        {
            var key = $"{Constants.Check}-{payload.PullRequest.User}";

            var checks = await appState.ReadState<List<long>>(key);

            checks = checks ?? new List<long>();

            checks.Add(check.Id);

            return checks;
        }
    }
}
