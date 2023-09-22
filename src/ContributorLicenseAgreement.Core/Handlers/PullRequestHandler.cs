/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.Aad;
    using GitOps.Clients.Ospo;
    using Microsoft.Extensions.Logging;
    using Check = ContributorLicenseAgreement.Core.Handlers.Model.Check;

    internal class PullRequestHandler : IAppEventHandler
    {
        private readonly ClaHelper claHelper;
        private readonly CheckHelper checkHelper;
        private readonly ILogger<CLA> logger;

        public PullRequestHandler(
            ClaHelper claHelper,
            CheckHelper checkHelper,
            ILogger<CLA> logger)
        {
            this.claHelper = claHelper;
            this.checkHelper = checkHelper;
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

            var primitivesData = (IEnumerable<Cla>)parameters[0];
            if (!primitivesData.Any())
            {
                return appOutput;
            }

            var primitive = primitivesData.First();

            if (gitOpsPayload.PlatformContext.ActionType == PlatformEventActions.Closed)
            {
                appOutput.States = await checkHelper.CleanUpChecks(gitOpsPayload, primitive.Content);
                logger.LogInformation("Checks cleaned up");
                return appOutput;
            }

            if (gitOpsPayload.PullRequest.State == PullRequestState.Closed)
            {
                logger.LogInformation("Not acting on closed pull request");
                return appOutput;
            }

            await claHelper.RunCheck(gitOpsPayload, primitive, appOutput);

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
        }
    }
}
