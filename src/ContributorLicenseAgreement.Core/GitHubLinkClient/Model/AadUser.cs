/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.GitHubLinkClient.Model
{
    public class AadUser
    {
        public string Id { get; set; }

        public string Alias { get; set; }

        public string PreferredName { get; set; }

        public string UserPrincipalName { get; set; }

        public string EMailAddress { get; set; }
    }
}