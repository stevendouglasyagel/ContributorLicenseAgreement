namespace ContributorLicenseAgreement.Core
{
    using System.Diagnostics.CodeAnalysis;
    using ContributorLicenseAgreement.Core.Handlers;
    using GitOps.Apps.Abstractions;
    using GitOps.Clients.Azure.BlobStorage;
    using GitOps.Primitives;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    [ExcludeFromCodeCoverage]
    public sealed class ContributorLicenseAgreementStartup : AppStartupBase
    {
        /// <summary>
        /// This method is called when the app is initialized by the GitOps app server.
        /// Here the app can register items that will be added to the app's dependency
        /// injection container, just like a Startup.cs in a .NET web app.
        /// </summary>
        /// <param name="serviceCollection">Service collection.</param>
        /// <param name="configuration">Configuration.</param>
        public override void ConfigureServices(IServiceCollection serviceCollection, IConfiguration configuration)
        {
            var azureBlobSettings = configuration.GetSection(nameof(AzureBlobSettings)).Get<AzureBlobSettings>();
            serviceCollection.TryAddSingleton<IBlobStorage>(
                p => new BlobStorage(
                    azureBlobSettings.AccountName,
                    azureBlobSettings.AccountKey,
                    true));
            serviceCollection.AddSingleton<PrimitiveCollection>();
            serviceCollection.AddSingleton<PushHandler>();
            serviceCollection.AddSingleton<PullRequestHandler>();
        }
    }
}
