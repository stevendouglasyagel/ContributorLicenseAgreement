/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System;
    using System.Threading.Tasks;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using Octokit;

    public class LegacyClaCheckHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;

        public LegacyClaCheckHandler(IGitHubClientAdapterFactory factory)
        {
            this.factory = factory;
        }

        public PlatformEventActions EventType => PlatformEventActions.Check_Run;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (gitOpsPayload.PlatformContext.IsGitOpsTriggeredEvent)
            {
                return appOutput;
            }

            if (gitOpsPayload.CheckRun.Name.Equals(Constants.CheckName))
            {
                var client =
                    await factory.GetGitHubClientAdapterAsync(gitOpsPayload.PlatformContext.InstallationId, gitOpsPayload.PlatformContext.Dns);
                await client.UpdateCheckRunAsync(
                    long.Parse(gitOpsPayload.PlatformContext.RepositoryId),
                    gitOpsPayload.CheckRun.Id,
                    new CheckRunUpdate
                    {
                        Status = CheckStatus.Completed,
                        Conclusion = Enum.Parse<CheckConclusion>(Conclusion.Success.ToString(), true)
                    });
            }

            return appOutput;
        }
    }
}