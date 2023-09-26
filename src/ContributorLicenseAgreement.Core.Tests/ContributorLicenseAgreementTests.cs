/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Tests
{
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading.Tasks;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Apps.Abstractions.TestData;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    [ExcludeFromCodeCoverage]
    public class ContributorLicenseAgreementTests : IClassFixture<ClassFixture>
    {
        private readonly ClassFixture classFixture;

        public ContributorLicenseAgreementTests(ClassFixture classFixture)
        {
            this.classFixture = classFixture;
        }

        [Theory]
        [InlineData("user0")]
        [InlineData("user1")]
        [InlineData("externalUser0")]
        public async Task SignedCLAPrTest(string user)
        {
            var gitOpsPayload = (await GitOpsPayloadSamples.Generate(PlatformEventActions.Pull_Request)).First();
            gitOpsPayload.PullRequest.User = user;
            var app = classFixture.ServiceProvider.GetRequiredService<CLA>();

            var appOutput = await app.Run(gitOpsPayload);
            Assert.True(appOutput.Conclusion == Conclusion.Success);
            Assert.True(appOutput.Comment == null);
        }

        [Theory]
        [InlineData("formerUser0")]
        public async Task FirstTimeUserPrTest(string user)
        {
            var gitOpsPayload = (await GitOpsPayloadSamples.Generate(PlatformEventActions.Pull_Request)).First();
            gitOpsPayload.PullRequest.User = user;
            var app = classFixture.ServiceProvider.GetRequiredService<CLA>();

            var appOutput = await app.Run(gitOpsPayload);
            Assert.True(appOutput.Conclusion == Conclusion.Success);
            Assert.True(appOutput.Comment != null);
        }

        [Theory]
        [InlineData("@gitops-ppe agree")]
        [InlineData("@gitops-ppe rerun")]
        public async Task IssueCommentHandlerTest(string comment)
        {
            var appOutput = await Comment(comment);
            Assert.True(appOutput.Conclusion == Conclusion.Success);
            Assert.True(appOutput.Comment == null);
        }

        [Theory]
        [InlineData("@gitops-ppe dfjd")]
        public async Task IssueCommentHandlerErrorTest(string comment)
        {
            var appOutput = await Comment(comment);
            Assert.True(appOutput.Conclusion == Conclusion.Success);
            Assert.True(appOutput.Comment != null);
        }

        [Fact]
        public async Task IssueCommentHandlerTerminateTest()
        {
            var appOutput = await Comment("@gitops-ppe terminate");
            Assert.True(appOutput.Conclusion == Conclusion.Success);
            Assert.True(appOutput.Comment != null);
        }

        [Fact]
        public async Task PushHandlerTest()
        {
            var gitOpsPayload = new GitOpsPayload
            {
                PlatformContext = new PlatformContext
                {
                    Dns = "microsoft.githubenterprise.com",
                    RepositoryId = "1223",
                    RepositoryName = "test",
                    ActionType = PlatformEventActions.Push,
                    EventType = PlatformEventActions.Push,
                    DefaultBranchName = "main"
                },
                Push = new Push
                {
                    BranchName = "main",
                    RepositoryDefaultBranch = "main",
                    RepositoryName = "cla-test",
                    Files = new List<PullRequestFile>
                    {
                        new PullRequestFile
                        {
                            FileName = "approvedUsers.csv",
                            ContentAfterChange = "test-employee",
                            ContentBeforeChange = "test-ex-employee"
                        }
                    }
                }
            };

            var app = classFixture.ServiceProvider.GetRequiredService<CLA>();
            var appOutput = await app.Run(gitOpsPayload);

            Assert.Equal(Conclusion.Success, appOutput.Conclusion);
            Assert.NotNull(appOutput.States);
            Assert.Contains("test-employee-httpstest3.yml", appOutput.States.StateCollection.Keys);
            Assert.Contains("test-ex-employee-httpstest3.yml", appOutput.States.StateCollection.Keys);
        }

        private async Task<AppOutput> Comment(string comment)
        {
            var gitOpsPayload = new GitOpsPayload
            {
                PlatformContext = new PlatformContext
                {
                    Dns = "microsoft.githubenterprise.com",
                    RepositoryId = "1223",
                    RepositoryName = "test",
                    ActionType = PlatformEventActions.Issue_Comment,
                    EventType = PlatformEventActions.Issue_Comment
                },
                PullRequestComment = new PullRequestComment
                {
                    Body = comment,
                    User = "user0",
                    RepositoryId = "1223"
                }
            };

            var app = classFixture.ServiceProvider.GetRequiredService<CLA>();

            return await app.Run(gitOpsPayload);
        }
    }
}
