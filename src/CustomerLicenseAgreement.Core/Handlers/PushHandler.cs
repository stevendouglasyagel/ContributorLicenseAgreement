namespace CustomerLicenseAgreement.Core.Handlers
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using GitOps.Abstractions;
    using GitOps.Apps.Abstractions.AppEventHandler;
    using GitOps.Apps.Abstractions.Models;
    using GitOps.Apps.Abstractions.Models.Issue;

    public sealed class PushHandler : IAppEventHandler
    {
        public PushHandler()
        {
            // Do nothing
        }

        public PlatformEventActions EventType => PlatformEventActions.Push;

        public async Task<object> HandleEvent(GitOpsPayload gitOpsPayload, AppOutput appOutput, params object[] parameters)
        {
            return await Task.FromResult(appOutput);
        }
    }
}
