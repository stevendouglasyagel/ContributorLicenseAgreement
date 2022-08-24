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
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppStates;
    using GitOps.Apps.Abstractions.Models;
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
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly ILogger<CLA> logger;

        public GitHubHelper(
            IGitHubClientAdapterFactory factory,
            AppState appState,
            PlatformAppFlavorSettings flavorSettings,
            ILogger<CLA> logger)
        {
            this.factory = factory;
            this.appState = appState;
            this.flavorSettings = flavorSettings;
            this.logger = logger;
        }

        internal async Task UpdateChecksAsync(GitOpsPayload gitOpsPayload, bool hasCla, string gitHubUser)
        {
            var shas = await appState.ReadState<List<(long, string)>>($"{Constants.Check}-{gitHubUser}");

            if (shas == null)
            {
                return;
            }

            foreach (var (repoId, sha) in shas)
            {
                await CreateCheckAsync(gitOpsPayload, hasCla, repoId, sha);
            }
        }

        internal async Task<CheckRun> CreateCheckAsync(GitOpsPayload gitOpsPayload, bool hasCla, long repoId, string sha)
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
                repoId, check);
        }

        internal async Task<Comment> GenerateClaCommentAsync(ClaPrimitive primitive, GitOpsPayload payload, bool cla, string gitHubUser)
        {
            if (cla || payload.PlatformContext.ActionType == PlatformEventActions.Synchronize)
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

            return GenerateComment(
                $"{typeof(ContributorLicenseAgreement.Core.CLA).Namespace}.CLA.mustache", mustacheParams);
        }

        internal Comment GenerateFailureComment(string gitHubUser, string company)
        {
            var mustacheParams = new
            {
                User = gitHubUser,
                Company = company
            };

            return GenerateComment(
                $"{typeof(ContributorLicenseAgreement.Core.CLA).Namespace}.CLA-Error-Company.mustache", mustacheParams);
        }

        internal Comment GenerateFailureComment(GitOpsPayload payload, string gitHubUser)
        {
            var mustacheParams = new
            {
                User = gitHubUser,
                Bot = flavorSettings[payload.PlatformContext.Dns].Name
            };

            return GenerateComment(
                $"{typeof(ContributorLicenseAgreement.Core.CLA).Namespace}.CLA-Error.mustache", mustacheParams);
        }

        internal Comment GenerateComment(string fileName, object mustacheParams)
        {
            var name = fileName;
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
                MsftMail = msftMail,
                CanSelfTerminate = true
            };

            appOutput.States = GenerateStates(gitHubUser, cla);

            return cla;
        }

        internal States CreateClas(List<string> gitHubUsers, string company)
        {
            var dict = new System.Collections.Generic.Dictionary<string, object>();

            foreach (var gitHubUser in gitHubUsers)
            {
                var cla = new ContributorLicenseAgreement.Core.Handlers.Model.SignedCla
                {
                    Signed = System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Expires = null,
                    GitHubUser = gitHubUser,
                    Company = company,
                    CanSelfTerminate = false
                };
                dict.Add(gitHubUser, cla);
            }

            return new States
            {
                StateCollection = dict
            };
        }

        internal async Task<SignedCla> ExpireCla(string gitHubUser, bool user = true)
        {
            var cla = await appState.ReadState<ContributorLicenseAgreement.Core.Handlers.Model.SignedCla>(gitHubUser);
            if (!cla.CanSelfTerminate && user)
            {
                logger.LogError("This cla cannot be terminated by user {User}", gitHubUser);
                return null;
            }

            cla.Expires = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();

            return cla;
        }

        internal States GenerateStates(string gitHubUser, ContributorLicenseAgreement.Core.Handlers.Model.SignedCla cla)
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