/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace BranchProtectionUpdater
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CommandLine;
    using GitOps.Clients.Azure.Telemetry;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.ApplicationInsights;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualBasic.CompilerServices;

    public class Program
    {
        private const string Dns = "github.com";

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Arguments>(args).WithParsed(arg =>
            {
                var restClient = CreateGitHubAdapter(Dns, arg.PrivateKey);

                UpdateCheckRules(restClient, arg.OrgName, arg.RepoNames, arg.AppId, arg.BranchName);
            });
        }

        private static void UpdateCheckRules(
            IGitHubRestClient restClient,
            string orgName,
            IEnumerable<string> repoNames,
            int appId,
            string branchName)
        {
            foreach (var repoName in repoNames)
            {
                UpdateCheckRuleForRepo(restClient, orgName, repoName, appId, branchName);
            }
        }

        private static void UpdateCheckRuleForRepo(
            IGitHubRestClient restClient,
            string orgName,
            string repoName,
            int appId,
            string branchName)
        {
            var branchProtection = restClient.GetBranchProtection(orgName, repoName, branchName).Result;
            branchProtection.RequiredStatusChecks.Checks = branchProtection.RequiredStatusChecks.Checks.Where(c => c.Context.Equals("license/cla")).Select(c =>
                {
                    if (c.AppId != appId)
                    {
                        c.AppId = appId;
                    }

                    return c;
                }).Union(branchProtection.RequiredStatusChecks.Checks.Where(c => !c.Context.Equals("license/cla")))
                .ToList();
            restClient.UpdateStatusCheckProtection(orgName, repoName, branchName, branchProtection.RequiredStatusChecks).Wait();
            Console.WriteLine($"Updated BranchProtectionRule for {orgName}/{repoName}:{branchName}");
        }

        private static IGitHubRestClient CreateGitHubAdapter(string gitHubUri, string gitHubToken)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging(configure => configure.AddConsole());
            serviceCollection.AddSingleton<IAppTelemetry>(_ => new AppTelemetry(nameof(Program), new TelemetryClient(
                new TelemetryConfiguration(Guid.NewGuid().ToString()))));

            var platformAppFlavorSettings = new PlatformAppFlavorSettings
            {
                PlatformAppsSettings = new Dictionary<string, PlatformAppSettings>
                {
                    {
                        gitHubUri,
                        new PlatformAppSettings
                        {
                            Name = nameof(Program), EnterpriseUrl = $"https://{gitHubUri}", EnterpriseApiRoot = "https://api.github.com/"
                        }
                    }
                }
            };
            serviceCollection.AddGitHubClientFactory(platformAppFlavorSettings);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            var gitHubClientAdapterFactory = serviceProvider.GetService<IGitHubClientAdapterFactory>();

            if (gitHubClientAdapterFactory == null)
            {
                throw new IncompleteInitialization();
            }

            return gitHubClientAdapterFactory
                .GetGitHubRestClientWithPatAsync(gitHubToken, gitHubUri);
        }
    }
}