/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers.Model
{
    public class SignedCla
    {
        public string GitHubUser { get; set; }

        public string Company { get; set; }

        public string MsftMail { get; set; }

        public bool Employee { get; set; }

        public long Signed { get; set; }

        public long? Expires { get; set; }

        public bool CanSelfTerminate { get; set; }

        public override string ToString()
        {
            var user = $"User: {GitHubUser}";
            user = Company == null || Company.Equals(string.Empty) ? user : user + $", Company: {Company}";
            return Employee ? $"{user}, eMail: {MsftMail}" : user;
        }

        public string GetParsableLog()
        {
            var company = Company == null || Company.Equals(string.Empty) ? " " : Company;
            return $"{GitHubUser};{company};{MsftMail ?? " "}";
        }
    }
}