namespace CustomerLicenseAgreement.Core.Handlers
{
    using System.Threading.Tasks;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;

    internal class PullRequestHandler : IAppEventHandler
    {
        public PullRequestHandler()
        {
            // Do nothing
        }

        public PlatformEventActions EventType => PlatformEventActions.Pull_Request;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            // TODO: Implement app behaviour on failure
            return await Task.FromResult(appOutput);
        }
    }
}
