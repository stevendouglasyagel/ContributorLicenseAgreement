/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.Logging;

    public class IssueCommentHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly GitHubHelper gitHubHelper;
        private readonly ILogger<CLA> logger;

        public IssueCommentHandler(
            IGitHubClientAdapterFactory factory,
            PlatformAppFlavorSettings flavorSettings,
            GitHubHelper gitHubHelper,
            ILogger<CLA> logger)
        {
            this.factory = factory;
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

            if (!await CheckSenderAsync(gitOpsPayload))
            {
                return appOutput;
            }

            switch (ParseComment(gitOpsPayload.PullRequestComment.Body, gitOpsPayload.PlatformContext.Dns))
            {
                case CommentAction.Agree:
                    gitHubHelper.CreateCla(false, gitOpsPayload.PullRequestComment.User, appOutput);
                    await gitHubHelper.UpdateChecksAsync(gitOpsPayload, true, gitOpsPayload.PullRequestComment.User);
                    break;
                case CommentAction.Terminate:
                    await gitHubHelper.ExpireCla(gitOpsPayload.PullRequestComment.User, appOutput);
                    appOutput.Comment = await gitHubHelper.GenerateCommentAsync(primitive, gitOpsPayload, false, gitOpsPayload.PullRequestComment.User);
                    await gitHubHelper.UpdateChecksAsync(gitOpsPayload, false, gitOpsPayload.PullRequestComment.User);
                    break;
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

        private CommentAction ParseComment(string comment, string host)
        {
            var tokens = comment.Split(' ');

            if (tokens.Length == 2 && tokens.First().StartsWith($"@{flavorSettings[host].Name}"))
            {
                switch (tokens[1])
                {
                    case Constants.Agree:
                        return CommentAction.Agree;
                    case Constants.Terminate:
                        return CommentAction.Terminate;
                    default:
                        return CommentAction.Failure;
                }
            }

            return CommentAction.Failure;
        }
    }
}
