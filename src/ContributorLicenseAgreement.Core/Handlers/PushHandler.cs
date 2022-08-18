/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using Microsoft.Extensions.Logging;

    public class PushHandler : IAppEventHandler
    {
        private readonly GitHubHelper gitHubHelper;
        private readonly ILogger<CLA> logger;

        public PushHandler(
            GitHubHelper gitHubHelper,
            ILogger<CLA> logger)
        {
            this.gitHubHelper = gitHubHelper;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Push;

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

            if (!primitive.SignRepos.Any(r => r.RepoName.Equals(gitOpsPayload.Push.RepositoryName))
                || !gitOpsPayload.Push.Files.Any(f => f.FileName.Equals(Constants.FileName)))
            {
                return appOutput;
            }

            var file = gitOpsPayload.Push.Files.First(f => f.FileName.Equals(Constants.FileName));

            var (removals, additions) = GetDifferences(file);

            var states = gitHubHelper.CreateClas(
                additions, primitive.SignRepos.First(r => r.RepoName.Equals(gitOpsPayload.Push.RepositoryName)).CompanyName);

            foreach (var user in removals)
            {
                await gitHubHelper.UpdateChecksAsync(gitOpsPayload, false, user);
                states.StateCollection.Add(user, await gitHubHelper.ExpireCla(user));
            }

            foreach (var user in additions)
            {
                await gitHubHelper.UpdateChecksAsync(gitOpsPayload, true, user);
            }

            appOutput.States = states;
            return appOutput;
        }

        private (List<string>, List<string>) GetDifferences(PullRequestFile file)
        {
            var newUsers = file.ContentAfterChange != null
                ? file.ContentAfterChange.Split('\n').ToHashSet()
                : new HashSet<string>();
            var removals = new List<string>();
            if (file.ContentBeforeChange == null)
            {
                return (removals, newUsers.ToList());
            }

            var oldUsers = file.ContentBeforeChange.Split('\n').ToHashSet();

            foreach (var user in oldUsers)
            {
                if (newUsers.Contains(user))
                {
                    newUsers.Remove(user);
                }
                else
                {
                    removals.Add(user);
                }
            }

            return (removals, newUsers.ToList());
        }
    }
}