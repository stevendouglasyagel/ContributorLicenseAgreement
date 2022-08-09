/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.GitHubLinkClient.Model
{
    public class GitHubLink
    {
        public GitHubUser GitHub { get; set; }

        public AadUser Aad { get; set; }
    }
}