/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using Octokit;
    using Check = ContributorLicenseAgreement.Core.Handlers.Model.Check;

    public class CheckHelper
    {
        private readonly AppState appState;
        private readonly IGitHubClientAdapterFactory clientAdapterFactory;

        public CheckHelper(IGitHubClientAdapterFactory clientAdapterFactory, AppState appState)
        {
            this.appState = appState;
            this.clientAdapterFactory = clientAdapterFactory;
        }

        internal async Task UpdateChecksAsync(GitOpsPayload gitOpsPayload, bool hasCla, string gitHubUser, string claLink)
        {
            var shas = await appState.ReadState<List<Check>>($"{Constants.Check}-{ClaHelper.GenerateKey(gitHubUser, claLink)}");

            if (shas == null)
            {
                return;
            }

            foreach (var check in shas)
            {
                await CreateCheckAsync(gitOpsPayload, hasCla, check);
            }
        }

        internal async Task<CheckRun> CreateCheckAsync(GitOpsPayload gitOpsPayload, bool hasCla, Check check)
        {
            var client = await clientAdapterFactory.GetGitHubClientAdapterAsync(
                check.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            var checkRun = new NewCheckRun(Constants.CheckName, check.Sha)
            {
                Status = hasCla ? CheckStatus.Completed : CheckStatus.Queued,
                Output = new NewCheckRunOutput(hasCla ? Constants.CheckSuccessTitle : Constants.CheckInProgressTitle, Constants.CheckSummary)
            };

            if (hasCla)
            {
                checkRun.Conclusion = Enum.Parse<CheckConclusion>(Conclusion.Success.ToString(), true);
            }

            return await client.CreateCheckRunAsync(check.RepoId, checkRun);
        }

        internal async Task<States> CleanUpChecks(GitOpsPayload payload, string claLink)
        {
            var key = $"{Constants.Check}-{ClaHelper.GenerateKey(payload.PullRequest.User, claLink)}";
            var checks = await appState.ReadState<List<Check>>(key);
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
                        checks.Where(s => !s.Sha.Equals(payload.PullRequest.Sha)).ToList()
                    }
                }
            };
        }

        internal async Task<List<Check>> AddCheckToStatesAsync(GitOpsPayload payload, Check check, string claLink)
        {
            var key = $"{Constants.Check}-{ClaHelper.GenerateKey(payload.PullRequest.User, claLink)}";

            var checks = await appState.ReadState<List<Check>>(key);

            checks = checks ?? new List<Check>();

            checks.Add(check);

            return checks;
        }
    }
}