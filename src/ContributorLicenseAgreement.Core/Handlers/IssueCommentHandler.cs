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
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.Logging;
    using Octokit;

    public class IssueCommentHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly AppState appState;
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly ILogger<CLA> logger;

        public IssueCommentHandler(
            IGitHubClientAdapterFactory factory,
            AppState appState,
            PlatformAppFlavorSettings flavorSettings,
            ILogger<CLA> logger)
        {
            this.factory = factory;
            this.appState = appState;
            this.flavorSettings = flavorSettings;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Issue_Comment;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (gitOpsPayload.PlatformContext.IsGitOpsTriggeredEvent)
            {
                return appOutput;
            }

            if (!await CheckSender(gitOpsPayload))
            {
                return appOutput;
            }

            if (ParseComment(gitOpsPayload.PullRequestComment.Body, gitOpsPayload.PlatformContext.Dns))
            {
                await UpdateChecks(gitOpsPayload);
            }

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
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

        private async Task<bool> CheckSender(GitOpsPayload gitOpsPayload)
        {
            var client = await factory.GetGitHubClientAdapterAsync(
                gitOpsPayload.PlatformContext.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            var pr = await client.GetPullRequestAsync(
                long.Parse(gitOpsPayload.PlatformContext.RepositoryId),
                gitOpsPayload.PullRequestComment.PullRequestNumber);

            return pr.User.Login.Equals(gitOpsPayload.PullRequestComment.User);
        }

        private async Task UpdateChecks(GitOpsPayload gitOpsPayload)
        {
            var client = await factory.GetGitHubClientAdapterAsync(
                gitOpsPayload.PlatformContext.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            var checkIds = await appState.ReadState<List<long>>($"{Constants.Check}-{gitOpsPayload.PullRequestComment.User}");

            foreach (var checkId in checkIds)
            {
                await client.UpdateCheckRunAsync(
                    long.Parse(gitOpsPayload.PullRequestComment.RepositoryId),
                    checkId,
                    new CheckRunUpdate
                    {
                        Status = CheckStatus.Completed,
                        Conclusion = Enum.Parse<CheckConclusion>(Conclusion.Success.ToString(), true),
                        Output = new NewCheckRunOutput("cla", string.Empty)
                    });
            }
        }
    }
}