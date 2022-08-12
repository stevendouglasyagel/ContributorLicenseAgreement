/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.GitHubLinkClient
{
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.GitHubLinkClient.Model;

    public interface IGitHubLinkRestClient
    {
        public Task<GitHubLink> GetLink(string gitHubUser);
    }
}