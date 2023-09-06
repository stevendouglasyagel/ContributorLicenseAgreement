/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Model
{
    public class Check
    {
        public string Sha { get; set; }

        public long InstallationId { get; set; }

        public long RepoId { get; set; }
    }
}