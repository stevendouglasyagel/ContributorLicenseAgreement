/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Threading.Tasks;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using Microsoft.Extensions.Logging;

    public class LegacyClaCommentHandler : IAppEventHandler
    {
        private readonly LegacyClaSettings legacyClaSettings;
        private readonly IGitHubClientAdapterFactory factory;
        private readonly ILogger<CLA> logger;

        public LegacyClaCommentHandler(LegacyClaSettings legacyClaSettings, IGitHubClientAdapterFactory factory, ILogger<CLA> logger)
        {
            this.legacyClaSettings = legacyClaSettings;
            this.factory = factory;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Issue_Comment;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (parameters.Length == 0)
            {
                logger.LogInformation("No primitive available");
                return appOutput;
            }

            if (!legacyClaSettings.Enabled)
            {
                return appOutput;
            }

            if (gitOpsPayload.PullRequestComment == null)
            {
                return appOutput;
            }

            if (gitOpsPayload.PullRequestComment.Action == PlatformEventActions.Deleted)
            {
                return appOutput;
            }

            if (!gitOpsPayload.PullRequestComment.User.Equals($"{legacyClaSettings.AppName}[bot]"))
            {
                logger.LogInformation("Not acting on comment from {Name}", gitOpsPayload.PullRequestComment.User);
                return appOutput;
            }

            logger.LogInformation("Deleting comment from {Name}", gitOpsPayload.PullRequestComment.User);
            var client = await factory.GetGitHubClientAdapterAsync(
                gitOpsPayload.PlatformContext.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            await client.DeleteIssueCommentAsync(
                long.Parse(gitOpsPayload.PlatformContext.RepositoryId),
                gitOpsPayload.PullRequestComment.Id);

            appOutput.Conclusion = Conclusion.Success;
            return appOutput;
        }
    }
}