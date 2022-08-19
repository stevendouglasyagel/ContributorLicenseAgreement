/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using ContributorLicenseAgreement.Core.GitHubLinkClient;
    using ContributorLicenseAgreement.Core.GitHubLinkClient.Model;
    using ContributorLicenseAgreement.Core.Handlers;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using GitOps.Apps.Abstractions;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Extensions;
    using GitOps.Clients.Aad;
    using GitOps.Clients.Azure.BlobStorage;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using GitOps.Primitives;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Moq;
    using Octokit;

    public sealed class ClassFixture : IDisposable
    {
        public ClassFixture()
        {
            var mockBlobStorage = new Mock<IBlobStorage>();
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "user0"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = true, Expires = null, Signed = 1, GitHubUser = "user0", MsftMail = "user0@microsoft.com", CanSelfTerminate = true } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "formerUser0"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = true, Expires = null, Signed = 1, GitHubUser = "formerUser0", MsftMail = "formerUser0@microsoft.com" } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "externalUser0"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = false, Expires = null, Signed = 1, GitHubUser = "externalUser0", MsftMail = null } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "test-ex-employee"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = new SignedCla { Employee = false, Expires = null, Signed = 1, GitHubUser = "test-ex-employee", MsftMail = null, Company = "test" } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<SignedCla>>("AppStates", It.IsAny<string>(), "user1"))
                .ReturnsAsync(new AppStateTableEntity<SignedCla> { State = null });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<(long, string)>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-externalUser0"))
                .ReturnsAsync(new AppStateTableEntity<List<(long, string)>> { State = new List<(long, string)> { (1, "sha") } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<(long, string)>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-user0"))
                .ReturnsAsync(new AppStateTableEntity<List<(long, string)>> { State = new List<(long, string)> { (1, "sha") } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<(long, string)>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-formerUser0"))
                .ReturnsAsync(new AppStateTableEntity<List<(long, string)>> { State = new List<(long, string)> { (1, "sha") } });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<(long, string)>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-user1"))
                .ReturnsAsync(new AppStateTableEntity<List<(long, string)>> { State = null });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<(long, string)>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-test-employee"))
                .ReturnsAsync(new AppStateTableEntity<List<(long, string)>> { State = null });
            mockBlobStorage.Setup(f =>
                    f.ReadTableEntityAsync<AppStateTableEntity<List<(long, string)>>>("AppStates", It.IsAny<string>(), $"{Constants.Check}-test-ex-employee"))
                .ReturnsAsync(new AppStateTableEntity<List<(long, string)>> { State = null });
            mockBlobStorage.Setup(f => f.DownloadBlob(It.IsAny<string>(), It.IsAny<Uri>()))
                .ReturnsAsync(File.ReadAllText("Data/cla.yml"));
            mockBlobStorage.Setup(f => f.ListBlobs(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new List<Uri> { new Uri("http://test") });

            var mockGitHubLinkClient = new Mock<IGitHubLinkRestClient>();
            mockGitHubLinkClient.Setup(f => f.GetLink("user1"))
                .ReturnsAsync(new GitHubLink { GitHub = new GitHubUser { Id = 1, Login = "user1" }, Aad = new AadUser { Alias = "user1", UserPrincipalName = "user1@microsoft.com" } });

            var mockAadClient = new Mock<IAadRequestClient>();
            mockAadClient.Setup(f => f.ResolveUserAsync("user0@microsoft.com")).ReturnsAsync(new ResolvedUser("test"));
            mockAadClient.Setup(f => f.ResolveUserAsync("user1@microsoft.com")).ReturnsAsync(new ResolvedUser("test"));
            mockAadClient.Setup(f => f.ResolveUserAsync("formerUser0@microsoft.com")).ReturnsAsync(new ResolvedUser());

            var mockLogger = new Mock<ILogger<CLA>>();
            mockLogger.Setup(x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception, string>)It.IsAny<object>()));

            var checkRun = new CheckRun(
                1,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                CheckStatus.Completed,
                null,
                new DateTimeOffset(1994, 3, 6, 12, 01, 13, TimeSpan.Zero),
                null,
                null,
                null,
                null,
                null,
                null);

            var mockClient = new Mock<IGitHubClientAdapter>();
            mockClient.Setup(f =>
                f.UpdateCheckRunAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CheckRunUpdate>()));
            mockClient.Setup(f =>
                f.CreateCheckRunAsync(It.IsAny<long>(), It.IsAny<NewCheckRun>())).ReturnsAsync(checkRun);
            mockClient.Setup(f => f.GetPullRequestAsync(It.IsAny<long>(), It.IsAny<int>()))
                .ReturnsAsync(new PullRequest(
                    1,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    1,
                    ItemState.Open,
                    string.Empty,
                    string.Empty,
                    DateTimeOffset.Now,
                    DateTimeOffset.Now,
                    null,
                    null,
                    null,
                    null,
                    new User(
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        1,
                        string.Empty,
                        DateTimeOffset.Now,
                        DateTimeOffset.Now,
                        1,
                        string.Empty,
                        1,
                        1,
                        false,
                        string.Empty,
                        1,
                        1,
                        string.Empty,
                        "user0",
                        string.Empty,
                        string.Empty,
                        1,
                        null,
                        1,
                        1,
                        1,
                        string.Empty,
                        null,
                        false,
                        string.Empty,
                        null),
                    null,
                    null,
                    false,
                    false,
                    null,
                    null,
                    string.Empty,
                    1,
                    1,
                    1,
                    1,
                    1,
                    null,
                    false,
                    null,
                    null,
                    null,
                    null));

            var mockFactory = new Mock<IGitHubClientAdapterFactory>();
            mockFactory.Setup(f => f.GetGitHubClientAdapterAsync(It.IsAny<long>(), It.IsAny<string>()))
                .ReturnsAsync(mockClient.Object);

            var platformAppFlavorSettings = new PlatformAppFlavorSettings
            {
                PlatformAppsSettings = new Dictionary<string, PlatformAppSettings>
                {
                    {
                        "microsoft.githubenterprise.com",
                        new PlatformAppSettings
                        {
                            Name = "gitops-ppe"
                        }
                    }
                }
            };

            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<PrimitiveCollection>();
            serviceCollection.RegisterAppEventHandlerOrchestrator();
            serviceCollection.AddSingleton<ILogger<CLA>>(mockLogger.Object);
            serviceCollection.AddSingleton<IAadRequestClient>(mockAadClient.Object);
            serviceCollection.AddSingleton<IGitHubClientAdapterFactory>(mockFactory.Object);
            serviceCollection.AddSingleton<IGitHubLinkRestClient>(mockGitHubLinkClient.Object);
            serviceCollection.AddSingleton<IBlobStorage>(mockBlobStorage.Object);
            serviceCollection.AddSingleton(platformAppFlavorSettings);

            ServiceProvider = serviceCollection.BuildServiceProvider();
            var appState = new AppState(mockBlobStorage.Object, new Lazy<AppBase>(() => ServiceProvider.GetRequiredService<CLA>()));

            serviceCollection.AddSingleton(appState);
            serviceCollection.AddSingleton<PullRequestHandler>();
            serviceCollection.AddSingleton<IssueCommentHandler>();
            serviceCollection.AddSingleton<GitHubHelper>();
            serviceCollection.AddSingleton<CLA>();

            ServiceProvider = serviceCollection.BuildServiceProvider();
        }

        internal ServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
            ServiceProvider?.Dispose();
        }
    }
}