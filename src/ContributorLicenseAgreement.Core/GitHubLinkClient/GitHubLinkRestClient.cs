/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.GitHubLinkClient
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.GitHubLinkClient.Model;
    using Polly;
    using Polly.Retry;

    [ExcludeFromCodeCoverage]
    public class GitHubLinkRestClient : IGitHubLinkRestClient
    {
        private readonly HttpClient httpClient;
        private readonly string apiVersion;

        public GitHubLinkRestClient(OspoGitHubLinkSettings ospoGitHubLinkSettings, IHttpClientFactory httpClientFactory)
        {
            httpClient = httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(ospoGitHubLinkSettings.ApiUrl);
            var authInfo = $":{ospoGitHubLinkSettings.ApiToken}";
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.Default.GetBytes(authInfo)));
            this.apiVersion = ospoGitHubLinkSettings.ApiVersion;
        }

        public async Task<GitHubLink> GetLink(string gitHubUser)
        {
            var route = $"links/github/{gitHubUser}?api-version={apiVersion}";
            HttpResponseMessage response = null;
            await Policy
                .Handle<Exception>()
                .OrResult<HttpResponseMessage>(r => r == null)
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)))
                .ExecuteAsync(async () =>
                {
                    response = await httpClient.GetAsync(route);
                    return response;
                });

            return await response.Content.ReadAsAsync<GitHubLink>();
        }
    }
}