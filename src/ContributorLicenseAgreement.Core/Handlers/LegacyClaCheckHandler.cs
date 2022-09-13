/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitHubJwt;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.Azure.Telemetry;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.Logging;
    using CommitState = GitOps.Abstractions.CommitStatus.CommitState;
    using CommitStatus = GitOps.Clients.GitHub.Models.CommitStatus;

    public class LegacyClaCheckHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly PlatformAppFlavorSettings appFlavorSettings;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly LegacyClaSettings legacyClaSettings;
        private readonly ILogger<CLA> logger;

        public LegacyClaCheckHandler(
            IGitHubClientAdapterFactory factory,
            PlatformAppFlavorSettings appFlavorSettings,
            IHttpClientFactory httpClientFactory,
            LegacyClaSettings legacyClaSettings,
            ILogger<CLA> logger)
        {
            this.factory = factory;
            this.appFlavorSettings = appFlavorSettings;
            this.httpClientFactory = httpClientFactory;
            this.legacyClaSettings = legacyClaSettings;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Status;

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

            if (!legacyClaSettings.Enabled)
            {
                return appOutput;
            }

            if (gitOpsPayload.PlatformContext.IsGitOpsTriggeredEvent)
            {
                return appOutput;
            }

            if (gitOpsPayload.CommitStatusUpdate.Context.Equals(Constants.CheckName)
                && gitOpsPayload.CommitStatusUpdate.CommitState != CommitState.Success)
            {
                logger.LogInformation("Stauts received for {Name}", gitOpsPayload.CommitStatusUpdate.Context);

                var jwtFactory = new GitHubJwtFactory(
                    new StringPrivateKeySource(legacyClaSettings.PrivateKey),
                    new GitHubJwtFactoryOptions
                    {
                        AppIntegrationId = legacyClaSettings.AppId, // The GitHub App Id
                        ExpirationSeconds = 590 // 10 minutes is the maximum time allowed
                    });
                var dict = new Dictionary<string, IGitHubJwtFactory> { { gitOpsPayload.PlatformContext.Dns, jwtFactory } };
                var ghFactory = new GitHubClientAdapterFactory(dict, appFlavorSettings, new AppTelemetry(null, null), httpClientFactory);
                var client =
                    await ghFactory.GetGitHubRestClientAsync(gitOpsPayload.PlatformContext.OrganizationName, gitOpsPayload.PlatformContext.Dns);
                await client.CreateCommitStatus(
                    gitOpsPayload.PlatformContext.OrganizationName,
                    gitOpsPayload.PlatformContext.RepositoryName,
                    gitOpsPayload.CommitStatusUpdate.Sha,
                    new CommitStatus
                    {
                        State = "success",
                        Description = Constants.CheckSuccessTitle,
                        Context = Constants.CheckName
                    });
                logger.LogInformation("Check run updated for {Name}", gitOpsPayload.CheckRun.Name);
            }

            return appOutput;
        }
    }
}