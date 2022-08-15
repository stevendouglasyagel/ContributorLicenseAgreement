/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.GitHubLinkClient;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.Aad;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.Logging;
    using Octokit;
    using Stubble.Core.Builders;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    public class GitHubHelper
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly AppState appState;
        private readonly IAadRequestClient aadRequestClient;
        private readonly IGitHubLinkRestClient gitHubLinkClient;
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly ILogger<CLA> logger;

        public GitHubHelper(
            IGitHubClientAdapterFactory factory,
            AppState appState,
            IAadRequestClient aadRequestClient,
            IGitHubLinkRestClient gitHubLinkClient,
            PlatformAppFlavorSettings flavorSettings,
            ILogger<CLA> logger)
        {
            this.factory = factory;
            this.appState = appState;
            this.aadRequestClient = aadRequestClient;
            this.gitHubLinkClient = gitHubLinkClient;
            this.flavorSettings = flavorSettings;
            this.logger = logger;
        }

        internal async Task UpdateChecksAsync(GitOpsPayload gitOpsPayload, bool hasCla, string gitHubUser)
        {
            var shas = await appState.ReadState<List<string>>($"{Constants.Check}-{gitHubUser}");

            foreach (var sha in shas)
            {
                await CreateCheckAsync(gitOpsPayload, hasCla, sha);
            }
        }

        internal async Task<CheckRun> CreateCheckAsync(GitOpsPayload gitOpsPayload, bool hasCla, string sha)
        {
            var client = await factory.GetGitHubClientAdapterAsync(
                gitOpsPayload.PlatformContext.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            var check = new NewCheckRun(Constants.CheckName, sha)
            {
                Status = hasCla ? CheckStatus.Completed : CheckStatus.Queued,
                Output = new NewCheckRunOutput(hasCla ? Constants.CheckSuccessTitle : Constants.CheckInProgressTitle, Constants.CheckSummary)
            };

            if (hasCla)
            {
                check.Conclusion = Enum.Parse<CheckConclusion>(Conclusion.Success.ToString(), true);
            }

            return await client.CreateCheckRunAsync(
                long.Parse(gitOpsPayload.PlatformContext.RepositoryId), check);
        }

        internal async Task<Comment> GenerateCommentAsync(ClaPrimitive primitive, GitOpsPayload payload, bool cla, string gitHubUser)
        {
            if (cla)
            {
                return null;
            }

            var response = await new HttpClient().GetAsync(primitive.ClaContent);
            var agreement = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
                .Deserialize<ClaAgreement>(await response.Content.ReadAsStringAsync());

            var mustacheParams = new
            {
                User = gitHubUser,
                CLA = agreement.Cla.Content,
                Bot = flavorSettings[payload.PlatformContext.Dns].Name
            };

            var name = $"{typeof(ContributorLicenseAgreement.Core.CLA).Namespace}.CLA.mustache";
            var tmp = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            var renderer = new StubbleBuilder().Build();
            using var stream = new StreamReader(tmp);
            var mustache = stream.ReadToEnd();
            var details = renderer.Render(
                mustache,
                mustacheParams);

            return new Comment
            {
                MarkdownText = details,
                CommentType = CommentType.RawComment
            };
        }

        internal Comment GenerateFailureComment(string gitHubUser)
        {
            var mustacheParams = new
            {
                User = gitHubUser
            };

            var name = $"{typeof(ContributorLicenseAgreement.Core.CLA).Namespace}.CLA-Error.mustache";
            var tmp = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
            var renderer = new StubbleBuilder().Build();
            using var stream = new StreamReader(tmp);
            var mustache = stream.ReadToEnd();
            var details = renderer.Render(
                mustache,
                mustacheParams);

            return new Comment
            {
                MarkdownText = details,
                CommentType = CommentType.RawComment
            };
        }

        internal SignedCla CreateCla(bool isEmployee, string gitHubUser, AppOutput appOutput, string company, string msftMail = null)
        {
            var cla = new ContributorLicenseAgreement.Core.Handlers.Model.SignedCla
            {
                Employee = isEmployee,
                Signed = System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Expires = null,
                GitHubUser = gitHubUser,
                Company = company,
                MsftMail = msftMail
            };

            appOutput.States = GenerateStates(gitHubUser, cla);

            return cla;
        }

        internal async Task<SignedCla> ExpireCla(string gitHubUser, AppOutput appOutput)
        {
            var cla = await appState.ReadState<ContributorLicenseAgreement.Core.Handlers.Model.SignedCla>(gitHubUser);
            cla.Expires = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();

            appOutput.States = GenerateStates(gitHubUser, cla);

            return cla;
        }

        private States GenerateStates(string gitHubUser, ContributorLicenseAgreement.Core.Handlers.Model.SignedCla cla)
        {
            {
                return new States
                {
                    StateCollection = new System.Collections.Generic.Dictionary<string, object> { { gitHubUser, cla } }
                };
            }
        }
    }
}