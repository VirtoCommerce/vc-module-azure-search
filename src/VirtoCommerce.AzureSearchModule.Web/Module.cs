using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.AzureSearchModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Extensions;

namespace VirtoCommerce.AzureSearchModule.Web
{
    public class Module : IModule, IHasConfiguration
    {
        public ManifestModuleInfo ModuleInfo { get; set; }
        public IConfiguration Configuration { get; set; }

        public void Initialize(IServiceCollection serviceCollection)
        {
            if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
            {
                serviceCollection.Configure<AzureSearchOptions>(Configuration.GetSection($"Search:{ModuleConstants.ProviderName}"));
                serviceCollection.AddSingleton<IAzureSearchDocumentsRequestBuilder, AzureSearchDocumentsRequestBuilder>();
                serviceCollection.AddSingleton<IAzureSearchDocumentsResponseBuilder, AzureSearchDocumentsResponseBuilder>();
                serviceCollection.AddSingleton<AzureSearchDocumentsProvider>();
            }
        }

        public void PostInitialize(IApplicationBuilder appBuilder)
        {
            var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
            settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

            if (Configuration.SearchProviderActive(ModuleConstants.ProviderName))
            {
                appBuilder.UseSearchProvider<AzureSearchDocumentsProvider>(ModuleConstants.ProviderName, (provider, documentTypes) =>
                {
                    provider.AddActiveAlias(documentTypes);
                });
            }
        }

        public void Uninstall()
        {
        }
    }
}
