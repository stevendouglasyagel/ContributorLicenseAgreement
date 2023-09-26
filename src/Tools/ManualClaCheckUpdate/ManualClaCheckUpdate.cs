/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ManualClaCheckUpdate
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Http;
    using CommandLine;
    using ContributorLicenseAgreement.Core;
    using GitHubJwt;
    using GitOps.Clients.Azure.Telemetry;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Octokit;

    [ExcludeFromCodeCoverage]
    public static class ManualClaCheckUpdate
    {
        private const string Dns = "github.com";

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Arguments>(args).WithParsed(arg =>
            {
                var serviceCollection = new ServiceCollection();
                serviceCollection.AddHttpClient();
                var serviceProvider = serviceCollection.BuildServiceProvider();

                var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

                UpdateClaCheck(
                    factory,
                    arg.OrgName,
                    arg.HeadSha,
                    arg.RepoId,
                    arg.PrivateKey,
                    arg.WebhookSecret,
                    arg.AppId,
                    arg.AppName);
            });
        }

        private static void UpdateClaCheck(
            IHttpClientFactory httpClientFactory,
            string orgName,
            string headSha,
            long repoId,
            string privateKey,
            string webhookSecret,
            string appId,
            string appName)
        {
            var appFlavorSettings = new PlatformAppFlavorSettings
            {
                PlatformAppsSettings = new Dictionary<string, PlatformAppSettings>
                {
                    {
                        Dns,
                        new PlatformAppSettings
                        {
                            Name = appName,
                            Id = appId,
                            PrivateKey = privateKey,
                            WebhookSecret = webhookSecret,
                            EnterpriseApiRoot = "https://api.github.com/"
                        }
                    }
                }
            };
            var jwtFactory = new GitHubJwtFactory(
                new StringPrivateKeySource(privateKey),
                new GitHubJwtFactoryOptions
                    {
                        AppIntegrationId = int.Parse(appId), // The GitHub App Id
                        ExpirationSeconds = 590 // 10 minutes is the maximum time allowed
                    });
            var dict = new Dictionary<string, IGitHubJwtFactory> { { Dns, jwtFactory } };
            var ghFactory = new GitHubClientAdapterFactory(dict, appFlavorSettings, new AppTelemetry(null, null), httpClientFactory);
            var client = ghFactory.GetGitHubClientAdapterAsync(orgName, Dns).Result;

            var checkRun = new NewCheckRun(Constants.CheckName, headSha)
            {
                Status = CheckStatus.Completed,
                Output = new NewCheckRunOutput(Constants.CheckSuccessTitle, Constants.CheckSummary),
                Conclusion = CheckConclusion.Success
            };
            client.CreateCheckRunAsync(repoId, checkRun).Wait();
        }
    }
}