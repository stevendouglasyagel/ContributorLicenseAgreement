/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace BranchProtectionUpdater
{
    using System.Collections.Generic;
    using CommandLine;

    internal sealed class Arguments
    {
        [Option('o', "orgName", Required = true, HelpText = "Org name owning the repo.")]
        public string OrgName { get; set; }

        [Option('g', "gitHubToken", Required = true, HelpText = "GitHube token.")]
        public string PrivateKey { get; set; }

        [Option('i', "appId", Required = true, HelpText = "App id.")]
        public int AppId { get; set; }

        [Option('b', "branchName", Required = true, HelpText = "The branch name for which the check should be changed.")]
        public string BranchName { get; set; }

        [Option('r', "repoNames", Required = true, HelpText = "Names of the repos for which the checks should be upgraded.")]
        public IEnumerable<string> RepoNames { get; set; }
    }
}