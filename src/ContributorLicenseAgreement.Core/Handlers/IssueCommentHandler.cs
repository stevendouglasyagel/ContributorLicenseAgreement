/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace ContributorLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using ContributorLicenseAgreement.Core.Handlers.Helpers;
    using ContributorLicenseAgreement.Core.Handlers.Model;
    using ContributorLicenseAgreement.Core.Primitives.Data;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Clients.GitHub;
    using GitOps.Clients.GitHub.Configuration;
    using Microsoft.Extensions.Logging;

    public class IssueCommentHandler : IAppEventHandler
    {
        private readonly IGitHubClientAdapterFactory factory;
        private readonly PlatformAppFlavorSettings flavorSettings;
        private readonly ClaHelper claHelper;
        private readonly CheckHelper checkHelper;
        private readonly CommentHelper commentHelper;
        private readonly ILogger<CLA> logger;

        public IssueCommentHandler(
            IGitHubClientAdapterFactory factory,
            PlatformAppFlavorSettings flavorSettings,
            ClaHelper claHelper,
            CheckHelper checkHelper,
            CommentHelper commentHelper,
            ILogger<CLA> logger)
        {
            this.factory = factory;
            this.flavorSettings = flavorSettings;
            this.claHelper = claHelper;
            this.checkHelper = checkHelper;
            this.commentHelper = commentHelper;
            this.logger = logger;
        }

        public PlatformEventActions EventType => PlatformEventActions.Issue_Comment;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            if (gitOpsPayload.PlatformContext.IsGitOpsTriggeredEvent)
            {
                return appOutput;
            }

            if (gitOpsPayload.PullRequestComment == null)
            {
                return appOutput;
            }

            if (parameters.Length == 0)
            {
                logger.LogInformation("No primitive available");
                return appOutput;
            }

            var primitivesData = (IEnumerable<ClaPrimitive>)parameters[0];
            if (!primitivesData.Any())
            {
                return appOutput;
            }

            var primitive = primitivesData.First();

            if (!await CheckSenderAsync(gitOpsPayload))
            {
                logger.LogInformation("Sender not pr author. Ignoring...");
                return appOutput;
            }

            var (commentAction, company) = ParseComment(gitOpsPayload.PullRequestComment.Body, gitOpsPayload.PlatformContext.Dns, primitive);
            SignedCla cla;
            switch (commentAction)
            {
                case CommentAction.Agree:
                    cla = claHelper.CreateCla(false, gitOpsPayload.PullRequestComment.User, appOutput, company, primitive.ClaContent);
                    await checkHelper.UpdateChecksAsync(gitOpsPayload, true, gitOpsPayload.PullRequestComment.User, primitive.ClaContent);
                    logger.LogInformation("CLA signed for GitHub-user: {Cla}", cla);
                    logger.LogInformation(
                        "Signing PR: {Org}/{Repo}: {Pr}",
                        gitOpsPayload.PlatformContext.OrganizationName,
                        gitOpsPayload.PlatformContext.RepositoryName,
                        gitOpsPayload.PullRequestComment.PullRequestNumber);
                    break;
                case CommentAction.Terminate:
                    cla = await claHelper.ExpireCla(gitOpsPayload.PullRequestComment.User, primitive.ClaContent);
                    if (cla == null)
                    {
                        break;
                    }

                    appOutput.States = claHelper.GenerateStates(gitOpsPayload.PullRequestComment.User, primitive.ClaContent, cla);
                    appOutput.Comment = await commentHelper.GenerateClaCommentAsync(primitive, gitOpsPayload, false, gitOpsPayload.PullRequestComment.User);
                    await checkHelper.UpdateChecksAsync(gitOpsPayload, false, gitOpsPayload.PullRequestComment.User, primitive.ClaContent);
                    logger.LogInformation("CLA terminated for GitHub-user: {Cla}", cla);
                    break;
                case CommentAction.Failure:
                    appOutput.Comment = commentHelper.GenerateFailureComment(gitOpsPayload, gitOpsPayload.PullRequestComment.User);
                    logger.LogInformation("Failed CLA sign attempt: {User}", gitOpsPayload.PullRequestComment.User);
                    break;
                case CommentAction.BlockedCompany:
                    appOutput.Comment = commentHelper.GenerateFailureComment(gitOpsPayload.PullRequestComment.User, company);
                    logger.LogInformation("Failed CLA sign attempt on behalf of company: {User}", gitOpsPayload.PullRequestComment.User);
                    break;
            }

            appOutput.Conclusion = Conclusion.Success;

            return appOutput;
        }

        private async Task<bool> CheckSenderAsync(GitOpsPayload gitOpsPayload)
        {
            var client = await factory.GetGitHubClientAdapterAsync(
                gitOpsPayload.PlatformContext.InstallationId,
                gitOpsPayload.PlatformContext.Dns);

            try
            {
                var pr = await client.GetPullRequestAsync(
                    long.Parse(gitOpsPayload.PlatformContext.RepositoryId),
                    gitOpsPayload.PullRequestComment.PullRequestNumber);
                return pr.User.Login.Equals(gitOpsPayload.PullRequestComment.User);
            }
            catch
            {
                logger.LogInformation(
                    "Unable to get pr {Number} for {Repo}",
                    gitOpsPayload.PullRequestComment.PullRequestNumber,
                    gitOpsPayload.PlatformContext.RepositoryName);
                return false;
            }
        }

        private (CommentAction, string) ParseComment(string comment, string host, ClaPrimitive primitive)
        {
            var pattern = @"[ ](?=(?:[^""]*""[^""]*"")*[^""]*$)";
            var regex = new Regex(pattern);
            var tokens = regex.Split(comment);

            CommentAction commentAction = CommentAction.Failure;

            if (tokens.Length >= 2 && tokens.First().StartsWith($"@{flavorSettings[host].Name}"))
            {
                switch (tokens[1])
                {
                    case Constants.Agree:
                        commentAction = CommentAction.Agree;
                        break;
                    case Constants.Terminate:
                        commentAction = CommentAction.Terminate;
                        break;
                }

                if (tokens.Length == 3)
                {
                    try
                    {
                        var companyInfo = tokens[2].Split('=');
                        if (companyInfo[0].Equals(Constants.Company))
                        {
                            var company = companyInfo[1].Replace("\"", string.Empty);
                            commentAction = primitive.ProhibitedCompanies != null
                                ? primitive.ProhibitedCompanies.Contains(company)
                                    ? CommentAction.BlockedCompany
                                    : commentAction
                                : commentAction;
                            return (commentAction, company);
                        }
                        else
                        {
                            commentAction = CommentAction.Failure;
                        }
                    }
                    catch
                    {
                        commentAction = CommentAction.Failure;
                    }
                }
            }
            else
            {
                commentAction = CommentAction.Noop;
            }

            return (commentAction, string.Empty);
        }
    }
}
