/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Helpers
{
    using System.IO;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub.Configuration;
    using Stubble.Core.Builders;
    using YamlDotNet.Serialization;
    using YamlDotNet.Serialization.NamingConventions;

    public class CommentHelper
    {
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly IHttpClientFactory httpClientFactory;

        public CommentHelper(PlatformAppFlavorSettings flavorSettings, IHttpClientFactory httpClientFactory)
        {
            this.flavorSettings = flavorSettings;
            this.httpClientFactory = httpClientFactory;
        }

        internal async Task<Comment> GenerateClaCommentAsync(ClaPrimitive primitive, GitOpsPayload payload, bool cla, string gitHubUser)
        {
            if (payload.PlatformContext.ActionType == PlatformEventActions.Synchronize)
            {
                return new Comment { KeepHistory = true };
            }

            if (cla)
            {
                return null;
            }

            var response = await httpClientFactory.CreateClient().GetAsync(primitive.ClaContent);
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
    }
}