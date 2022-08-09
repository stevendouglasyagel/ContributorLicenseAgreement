/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.GitHubLinkClient.Model
{
    using System.Collections.Generic;

    public class GitHubUser
    {
        public long Id { get; set; }

        public string Login { get; set; }

        public IEnumerable<string> Organizations { get; set; }
    }
}