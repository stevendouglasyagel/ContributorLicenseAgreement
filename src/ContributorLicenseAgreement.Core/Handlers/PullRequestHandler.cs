/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.GitHubLinkClient;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
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
    using PullRequest = GitOps.Abstractions.PullRequest;

    internal class PullRequestHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly AppState appState;
        private readonly IAadRequestClient aadRequestClient;
        private readonly GitHubLinkRestClient gitHubLinkClient;
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly ILogger<CLA> logger;

        public PullRequestHandler(
            IGitHubClientAdapterFactory factory,
            AppState appState,
            IAadRequestClient aadRequestClient,
            GitHubLinkRestClient gitHubLinkClient,
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

        public PlatformEventActions EventType => PlatformEventActions.Pull_Request;

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

            // ToDo: what happens if we have conflicting primitives?
            var primitive = primitivesData.First();

            if (NeedsLicense(primitive, gitOpsPayload.PullRequest))
            {
                // var cla = await HasSignedCla(appOutput, "JohannesLampel");
                var cla = await HasSignedCla(appOutput, gitOpsPayload.PullRequest.User);

                appOutput.Comment = await GenerateComment(primitive, gitOpsPayload, cla);

                var client = await factory.GetGitHubClientAdapterAsync(
                    gitOpsPayload.PlatformContext.InstallationId,
                    gitOpsPayload.PlatformContext.Dns);

                var check = await client.CreateCheckRunAsync(
                    long.Parse(gitOpsPayload.PullRequest.RepositoryId),
                    new NewCheckRun(Constants.CheckName, gitOpsPayload.PullRequest.Sha)
                    {
                        Status = CheckStatus.InProgress,
                        Output = new NewCheckRunOutput("cla", string.Empty)
                    });

                appOutput.States ??= new States
                {
                    StateCollection = new System.Collections.Generic.Dictionary<string, object>()
                };

                appOutput.States.StateCollection.Add(
                        $"{Constants.Check}-{gitOpsPayload.PullRequest.User}", await AddCheckToStates(check, gitOpsPayload));
            }

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
        }

        private static bool NeedsLicense(ClaPrimitive primitive, PullRequest pullRequest)
        {
            return !primitive.SkipUsers.Contains(pullRequest.Sender)
                   && !primitive.SkipOrgs.Contains(pullRequest.OrganizationName)
                   && pullRequest.Files.Sum(f => f.Changes) >= primitive.MinimalChangeRequired.CodeLines
                   && pullRequest.Files.Count >= primitive.MinimalChangeRequired.Files;
        }

        private async Task<Comment> GenerateComment(ClaPrimitive primitive, GitOpsPayload payload, bool cla)
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
                User = payload.PullRequest.Sender,
                Content = agreement.Cla.Content,
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

        private async Task<bool> HasSignedCla(AppOutput appOutput, string gitHubUser)
        {
            var cla = await appState.ReadState<ContributorLicenseAgreement.Core.Handlers.Model.SignedCla>(gitHubUser);

            if (cla == null)
            {
                // ToDo
                var gitHubLink = await gitHubLinkClient.GetLink(gitHubUser);
                if (gitHubLink.GitHub == null)
                {
                    return false;
                }

                var aadUser = await aadRequestClient.ResolveUserAsync(gitHubLink.Aad.UserPrincipalName);
                if (!aadUser.WasResolved)
                {
                    return false;
                }

                cla = new ContributorLicenseAgreement.Core.Handlers.Model.SignedCla
                {
                    Employee = true,
                    Signed = System.DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Expires = null,
                    GitHubUser = gitHubUser,
                    MsftMail = gitHubLink.Aad.UserPrincipalName
                };

                appOutput.States = GenerateStates(gitHubUser, cla);
            }

            if (!cla.Employee)
            {
                var timestamp = System.DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return cla.Expires > timestamp;
            }
            else
            {
                var user = await aadRequestClient.ResolveUserAsync(cla.MsftMail);
                return user.WasResolved;
            }
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

        private async Task<List<long>> AddCheckToStates(CheckRun check, GitOpsPayload payload)
        {
            var key = $"{Constants.Check}-{payload.PullRequest.User}";

            var checks = await appState.ReadState<List<long>>(key);

            checks = checks ?? new List<long>();

            checks.Add(check.Id);

            return checks;
        }
    }
}
