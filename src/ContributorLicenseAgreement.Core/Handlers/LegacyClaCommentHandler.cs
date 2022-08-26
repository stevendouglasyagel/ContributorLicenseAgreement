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

    public class LegacyClaCommentHandler : IAppEventHandler
    {
        private readonly LegacyClaSettings legacyClaSettings;
        private readonly IGitHubClientAdapterFactory factory;

        public LegacyClaCommentHandler(LegacyClaSettings legacyClaSettings, IGitHubClientAdapterFactory factory)
        {
            this.legacyClaSettings = legacyClaSettings;
            this.factory = factory;
        }

        public PlatformEventActions EventType => PlatformEventActions.Issue_Comment;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
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
                return appOutput;
            }

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