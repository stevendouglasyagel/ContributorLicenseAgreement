/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ManualClaCheckUpdate
{
    using System.Diagnostics.CodeAnalysis;
    using CommandLine;

    [ExcludeFromCodeCoverage]
    internal sealed class Arguments
    {
        [Option('o', "orgName", Required = true, HelpText = "Org name owning the repo.")]
        public string OrgName { get; set; }

        [Option('h', "headSha", Required = true, HelpText = "Sha for which to update the check.")]
        public string HeadSha { get; set; }

        [Option('r', "repoId", Required = true, HelpText = "Repo id of the repo for which the check should be updated.")]
        public long RepoId { get; set; }

        [Option('k', "privateKey", Required = true, HelpText = "App private key.")]
        public string PrivateKey { get; set; }

        [Option('s', "webhookSecret", Required = true, HelpText = "Webhook secret.")]
        public string WebhookSecret { get; set; }

        [Option('i', "appId", Required = true, HelpText = "App id.")]
        public string AppId { get; set; }

        [Option('n', "appName", Required = true, HelpText = "App name.")]
        public string AppName { get; set; }
    }
}