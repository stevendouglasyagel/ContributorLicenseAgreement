/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Threading.Tasks;
    using GitHubJwt;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.Azure.Telemetry;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Octokit;

    public class LegacyClaCheckHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly PlatformAppFlavorSettings appFlavorSettings;
        private readonly IHttpClientFactory httpClientFactory;
        private readonly LegacyClaSettings legacyClaSettings;

        public LegacyClaCheckHandler(
            IGitHubClientAdapterFactory factory,
            PlatformAppFlavorSettings appFlavorSettings,
            IHttpClientFactory httpClientFactory,
            LegacyClaSettings legacyClaSettings)
        {
            this.factory = factory;
            this.appFlavorSettings = appFlavorSettings;
            this.httpClientFactory = httpClientFactory;
            this.legacyClaSettings = legacyClaSettings;
        }

        public PlatformEventActions EventType => PlatformEventActions.Check_Run;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (!legacyClaSettings.Enabled)
            {
                return appOutput;
            }

            if (gitOpsPayload.PlatformContext.IsGitOpsTriggeredEvent)
            {
                return appOutput;
            }

            if (gitOpsPayload.CheckRun.Name.Equals(Constants.CheckName))
            {
                var tmpClient = await factory.GetGitHubRestClientAsync(
                    gitOpsPayload.PlatformContext.OrganizationName,
                    gitOpsPayload.PlatformContext.Dns);

                var installations = await tmpClient.GetOrgInstallations(gitOpsPayload.PlatformContext.OrganizationName);

                var installation = installations.InstallationsList.First(i => i.AppId == legacyClaSettings.AppId);

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
                    await ghFactory.GetGitHubClientAdapterAsync(installation.Id, gitOpsPayload.PlatformContext.Dns);
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