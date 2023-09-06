/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Helpers
{
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using Microsoft.Extensions.Logging;

    public class LoggingHelper
    {
        private readonly ILogger<CLA> logger;

        public LoggingHelper(ILogger<CLA> logger)
        {
            this.logger = logger;
        }

        public void LogClaSigned(SignedCla cla, string signer, string org = null, string repo = null, int pr = 0)
        {
            LogClaAction(cla, "cla_signed", signer, org, repo, pr);
        }

        public void LogClaTerminated(SignedCla cla, string signer, string org = null, string repo = null, int pr = 0)
        {
            LogClaAction(cla, "cla_terminated", signer, org, repo, pr);
        }

        private void LogClaAction(SignedCla cla, string action, string signer, string org = null, string repo = null, int pr = 0)
        {
            // todo
            var signLocation = org == null ? "pre-signed" : $"{org}/{repo}:{pr}";
            logger.LogInformation(
                "{Action};{Cla};{Signer};{SignLocation}",
                action,
                cla.GetParsableLog(),
                signer,
                signLocation);
        }
    }
}