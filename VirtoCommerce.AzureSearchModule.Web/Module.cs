using Microsoft.Practices.Unity;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model.Search;

namespace VirtoCommerce.AzureSearchModule.Web
{
    public class Module : ModuleBase
    {
        private readonly IUnityContainer _container;

        public Module(IUnityContainer container)
        {
            _container = container;
        }

        public override void Initialize()
        {
            base.Initialize();

            _container.RegisterType<ISearchProvider, AzureSearchProvider>(new ContainerControlledLifetimeManager());
            _container.RegisterType<ISearchQueryBuilder, AzureSearchQueryBuilder>();
        }
    }
}
