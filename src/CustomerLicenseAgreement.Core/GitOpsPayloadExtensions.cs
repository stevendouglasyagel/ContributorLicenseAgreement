/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace CustomerLicenseAgreement.Core
{
    using GitOps.Abstractions;

    internal static class GitOpsPayloadExtensions
    {
        internal static (string refBranchName, string branchName) GetBranchName(
            this GitOpsPayload gitOpsPayload)
        {
            if (gitOpsPayload.Push != null)
            {
                return (gitOpsPayload.Push.BranchName, ReadBranchName(gitOpsPayload.Push.BranchName));
            }

            return gitOpsPayload.PullRequest != null
                ? (gitOpsPayload.PullRequest.BranchName, ReadBranchName(gitOpsPayload.PullRequest.BranchName))
                : (gitOpsPayload.PlatformContext.DefaultBranchName, string.Empty);
        }

        private static string ReadBranchName(string refBranchName)
        {
            var idx = refBranchName.LastIndexOf('/');
            return idx == -1 ? refBranchName : refBranchName.Substring(idx + 1, refBranchName.Length - idx - 1);
        }
    }
}
