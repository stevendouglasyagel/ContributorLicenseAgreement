/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using Microsoft.Extensions.Logging;

    public class MergeGroupHandler : IAppEventHandler
    {
        private readonly ClaHelper claHelper;
        private readonly CheckHelper checkHelper;
        private readonly ILogger<CLA> logger;

        public MergeGroupHandler(
            ClaHelper claHelper,
            CheckHelper checkHelper,
            ILogger<CLA> logger)
        {
            this.claHelper = claHelper;
            this.checkHelper = checkHelper;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Merge_Group;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (parameters.Length == 0)
            {
                logger.LogInformation("No primitive available");
                return appOutput;
            }

            var prHandler = new PullRequestHandler(claHelper, checkHelper, logger);
            gitOpsPayload.PullRequest.Sha = gitOpsPayload.MergeGroup.HeadSha;
            return await prHandler.HandleEvent(gitOpsPayload, appOutput, parameters);
        }
    }
}