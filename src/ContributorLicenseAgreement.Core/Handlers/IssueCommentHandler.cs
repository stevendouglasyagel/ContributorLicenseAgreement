/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.Logging;

    public class IssueCommentHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly AppState appState;
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly GitHubHelper gitHubHelper;
        private readonly ILogger<CLA> logger;

        public IssueCommentHandler(
            IGitHubClientAdapterFactory factory,
            AppState appState,
            PlatformAppFlavorSettings flavorSettings,
            GitHubHelper gitHubHelper,
            ILogger<CLA> logger)
        {
            this.factory = factory;
            this.appState = appState;
            this.flavorSettings = flavorSettings;
            this.gitHubHelper = gitHubHelper;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Issue_Comment;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (gitOpsPayload.PlatformContext.IsGitOpsTriggeredEvent)
            {
                return appOutput;
            }

            if (!await CheckSenderAsync(gitOpsPayload))
            {
                return appOutput;
            }

            if (ParseComment(gitOpsPayload.PullRequestComment.Body, gitOpsPayload.PlatformContext.Dns))
            {
                gitHubHelper.CreateCla(false, gitOpsPayload.PullRequestComment.User, appOutput);
                await gitHubHelper.UpdateChecksAsync(gitOpsPayload, gitOpsPayload.PullRequestComment.User);
            }

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
        }

        private async Task<bool> CheckSenderAsync(GitOpsPayload gitOpsPayload)
        {
            var client = await factory.GetGitHubClientAdapterAsync(
                gitOpsPayload.PlatformContext.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            var pr = await client.GetPullRequestAsync(
                long.Parse(gitOpsPayload.PlatformContext.RepositoryId),
                gitOpsPayload.PullRequestComment.PullRequestNumber);

            return pr.User.Login.Equals(gitOpsPayload.PullRequestComment.User);
        }

        private bool ParseComment(string comment, string host)
        {
            var tokens = comment.Split(' ');

            if (tokens.Length > 3 || tokens.Length < 2)
            {
                return false;
            }

            return tokens.First().StartsWith($"@{flavorSettings[host].Name}") && tokens[1].Equals("--agree");
        }
    }
}
