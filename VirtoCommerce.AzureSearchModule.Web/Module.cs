﻿using Microsoft.Practices.Unity;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;

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

            var searchConnection = _container.Resolve<ISearchConnection>();

            if (searchConnection?.Provider?.EqualsInvariant("AzureSearch") == true)
            {
                _container.RegisterType<ISearchProvider, AzureSearchProvider>(new ContainerControlledLifetimeManager());
            }
        }
    }
}
