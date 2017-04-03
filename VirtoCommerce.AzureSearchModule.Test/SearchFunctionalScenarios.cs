using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using CacheManager.Core;
using Common.Logging;
using Microsoft.Xunit.Performance;
using Moq;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.CatalogModule.Data.Services;
using VirtoCommerce.CoreModule.Data.Repositories;
using VirtoCommerce.CoreModule.Data.Services;
using VirtoCommerce.Domain.Catalog.Services;
using VirtoCommerce.Domain.Commerce.Services;
using VirtoCommerce.Domain.Payment.Model;
using VirtoCommerce.Domain.Payment.Services;
using VirtoCommerce.Domain.Pricing.Services;
using VirtoCommerce.Domain.Shipping.Model;
using VirtoCommerce.Domain.Shipping.Services;
using VirtoCommerce.Domain.Store.Services;
using VirtoCommerce.Domain.Tax.Model;
using VirtoCommerce.Domain.Tax.Services;
using VirtoCommerce.Platform.Core.ChangeLog;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.Assets;
using VirtoCommerce.Platform.Data.ChangeLog;
using VirtoCommerce.Platform.Data.DynamicProperties;
using VirtoCommerce.Platform.Data.Infrastructure.Interceptors;
using VirtoCommerce.Platform.Data.Repositories;
using VirtoCommerce.PricingModule.Data.Repositories;
using VirtoCommerce.PricingModule.Data.Services;
using VirtoCommerce.SearchApiModule.Data.Model;
using VirtoCommerce.SearchApiModule.Data.Services;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model.Filters;
using VirtoCommerce.SearchModule.Core.Model.Indexing;
using VirtoCommerce.SearchModule.Core.Model.Search.Criterias;
using VirtoCommerce.SearchModule.Data.Services;
using VirtoCommerce.StoreModule.Data.Repositories;
using VirtoCommerce.StoreModule.Data.Services;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Test
{
    [Collection("Search")]
    public class SearchFunctionalScenarios : SearchTestsBase
    {
        private const string _scope = "test";
        private const string _storeName = "electronics";

        [Fact]
        public void Can_index_category_demo_data_and_search_using_outline()
        {
            var provider = GetSearchProvider();

            provider.RemoveAll(_scope, "");
            var controller = GetSearchIndexController(provider);
            controller.RemoveIndex(_scope, "category");
            controller.BuildIndex(_scope, "category", x => { });
            //controller.BuildIndex(scope, "category", x => { return; }, new[] { "0d4ad9bab9184d69a6e586effdf9c2ea" });

            // sleep for index to be commited
            Thread.Sleep(5000);

            // find all products in the category
            var categoryCriteria = new CategorySearchCriteria();

            categoryCriteria.Outlines.Add("4974648a41df4e6ea67ef2ad76d7bbd4");
            //categoryCriteria.Outlines.Add("4974648a41df4e6ea67ef2ad76d7bbd4/45d3fc9a913d4610a5c7d0470558*");

            var response = provider.Search<DocumentDictionary>(_scope, categoryCriteria);
            Assert.True(response.TotalCount > 0, "Didn't find any categories");
        }

        [Fact]
        public void Can_index_product_demo_data_and_search_using_outline()
        {
            var provider = GetSearchProvider();

            provider.RemoveAll(_scope, "");
            var controller = GetSearchIndexController(provider);
            controller.RemoveIndex(_scope, CatalogItemSearchCriteria.DocType);
            controller.BuildIndex(_scope, CatalogItemSearchCriteria.DocType, x => { });

            // sleep for index to be commited
            Thread.Sleep(5000);

            // get catalog id by name
            var catalogRepo = GetCatalogRepository();
            var catalog = catalogRepo.Catalogs.SingleOrDefault(x => x.Name.Equals(_storeName, StringComparison.OrdinalIgnoreCase));

            // find all products in the category
            var catalogCriteria = new CatalogItemSearchCriteria
            {
                Catalog = catalog?.Id,
                Currency = "USD"
            };

            catalogCriteria.Outlines.Add("4974648a41df4e6ea67ef2ad76d7bbd4/c76774f9047d4f18a916b38681c50557*");

            var ibs = GetItemBrowsingService(provider);
            var searchResults = ibs.SearchItems(_scope, catalogCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);

            Assert.True(searchResults.TotalCount > 0, "Didn't find any products");
        }

        [Fact]
        public void Can_index_product_demo_data_and_search()
        {
            var provider = GetSearchProvider();

            provider.RemoveAll(_scope, "");
            var controller = GetSearchIndexController(provider);
            controller.RemoveIndex(_scope, CatalogItemSearchCriteria.DocType);
            controller.BuildIndex(_scope, CatalogItemSearchCriteria.DocType, x => { });

            // sleep for index to be commited
            Thread.Sleep(5000);

            // get catalog id by name
            var catalogRepo = GetCatalogRepository();
            var catalog = catalogRepo.Catalogs.SingleOrDefault(x => x.Name.Equals(_storeName, StringComparison.OrdinalIgnoreCase));

            // find all products in the category
            var catalogCriteria = new CatalogItemSearchCriteria
            {
                Catalog = catalog?.Id,
                Currency = "USD"
            };

            // Add all filters
            var brandFilter = new AttributeFilter { Key = "brand" };
            var filter = new AttributeFilter
            {
                Key = "color",
                IsLocalized = true,
                Values = new[]
                {
                    new AttributeFilterValue {Id = "Red", Value = "Red"},
                    new AttributeFilterValue {Id = "Gray", Value = "Gray"},
                    new AttributeFilterValue {Id = "Black", Value = "Black"}
                }
            };

            var rangefilter = new RangeFilter
            {
                Key = "size",
                Values = new[]
                {
                    new RangeFilterValue {Id = "0_to_5", Lower = "0", Upper = "5"},
                    new RangeFilterValue {Id = "5_to_10", Lower = "5", Upper = "10"}
                }
            };

            var priceRangefilter = new PriceRangeFilter
            {
                Currency = "USD",
                Values = new[]
                {
                    new RangeFilterValue {Id = "under-100", Upper = "100"},
                    new RangeFilterValue {Id = "200-600", Lower = "200", Upper = "600"}
                }
            };

            catalogCriteria.Add(filter);
            catalogCriteria.Add(rangefilter);
            catalogCriteria.Add(priceRangefilter);
            catalogCriteria.Add(brandFilter);

            var ibs = GetItemBrowsingService(provider);
            var searchResults = ibs.SearchItems(_scope, catalogCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);

            Assert.True(searchResults.TotalCount > 0, "Didn't find any products");
            Assert.True(searchResults.Aggregations.Length > 0, "Didn't find any aggregations");

            var colorAggregation = searchResults.Aggregations.Single(a => a.Field.Equals("color", StringComparison.OrdinalIgnoreCase));
            Assert.True(colorAggregation.Items.Single(x => x.Value.ToString().Equals("Red", StringComparison.OrdinalIgnoreCase)).Count == 6);
            Assert.True(colorAggregation.Items.Single(x => x.Value.ToString().Equals("Gray", StringComparison.OrdinalIgnoreCase)).Count == 3);
            Assert.True(colorAggregation.Items.Single(x => x.Value.ToString().Equals("Black", StringComparison.OrdinalIgnoreCase)).Count == 13);

            var brandAggregation = searchResults.Aggregations.Single(a => a.Field.Equals("brand", StringComparison.OrdinalIgnoreCase));
            Assert.True(brandAggregation.Items.Single(x => x.Value.ToString().Equals("Beats By Dr Dre", StringComparison.OrdinalIgnoreCase)).Count == 3);

            var keywordSearchCriteria = new KeywordSearchCriteria(CatalogItemSearchCriteria.DocType) { Currency = "USD", Locale = "en-us", SearchPhrase = "sony" };
            searchResults = ibs.SearchItems(_scope, keywordSearchCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);
            Assert.True(searchResults.TotalCount > 0);
        }

        [Fact]
        public void Can_web_search_products()
        {
            var provider = GetSearchProvider();

            provider.RemoveAll(_scope, "");
            var controller = GetSearchIndexController(provider);
            controller.RemoveIndex(_scope, CatalogItemSearchCriteria.DocType);
            controller.BuildIndex(_scope, CatalogItemSearchCriteria.DocType, x => { });

            // sleep for index to be commited
            Thread.Sleep(5000);

            var storeRepo = GetStoreRepository();
            var storeObject = storeRepo.Stores.SingleOrDefault(x => x.Name.Equals(_storeName, StringComparison.OrdinalIgnoreCase));
            var store = GetStoreService().GetById(storeObject?.Id);

            // get catalog id by name
            var catalogRepo = GetCatalogRepository();
            var catalog = catalogRepo.Catalogs.SingleOrDefault(x => x.Name.Equals(_storeName, StringComparison.OrdinalIgnoreCase));

            // find all products in the category
            var criteria = new ProductSearch
            {
                Currency = "USD",
                Outline = "*" // find all products
                //Terms = new[] { "price:200-600" }
            };


            var context = new Dictionary<string, object>
            {
                { "Store", store },
            };

            var filterService = GetBrowseFilterService();
            var filters = filterService.GetFilters(context);
            var serviceCriteria = criteria.AsCriteria<CatalogItemSearchCriteria>(store.Id, store.Catalog, filters);
            var ibs = GetItemBrowsingService(provider);

            //Load ALL products 
            var searchResults = ibs.SearchItems(_scope, serviceCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);

            Assert.True(searchResults.TotalCount > 0, "Didn't find any products");
            Assert.True(searchResults.Aggregations.Length > 0, "Didn't find any aggregations");

            var colorAggregation = searchResults.Aggregations.SingleOrDefault(a => a.Field.Equals("color", StringComparison.OrdinalIgnoreCase));
            Assert.True(colorAggregation.Items.Where(x => x.Value.ToString().Equals("Red", StringComparison.OrdinalIgnoreCase)).SingleOrDefault().Count == 6);
            Assert.True(colorAggregation.Items.Where(x => x.Value.ToString().Equals("Gray", StringComparison.OrdinalIgnoreCase)).SingleOrDefault().Count == 3);
            Assert.True(colorAggregation.Items.Where(x => x.Value.ToString().Equals("Black", StringComparison.OrdinalIgnoreCase)).SingleOrDefault().Count == 13);

            var brandAggregation = searchResults.Aggregations.SingleOrDefault(a => a.Field.Equals("brand", StringComparison.OrdinalIgnoreCase));
            Assert.True(brandAggregation.Items.Where(x => x.Value.ToString().Equals("Beats By Dr Dre", StringComparison.OrdinalIgnoreCase)).SingleOrDefault().Count == 3);

            // now test sorting
            criteria = new ProductSearch
            {
                Currency = "USD",
                Outline = "*", // find all products
                Sort = new[] { "name" }
            };

            serviceCriteria = criteria.AsCriteria<CatalogItemSearchCriteria>(store.Id, store.Catalog, filters);
            searchResults = ibs.SearchItems(_scope, serviceCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);

            var productName = searchResults.Products[0].Name;
            Assert.True(productName == "3DR Solo Quadcopter (No Gimbal)");

            criteria = new ProductSearch
            {
                Currency = "USD",
                Outline = "*", // find all products
                Sort = new[] { "name-desc" }
            };

            serviceCriteria = criteria.AsCriteria<CatalogItemSearchCriteria>(store.Id, store.Catalog, filters);
            searchResults = ibs.SearchItems(_scope, serviceCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);

            productName = searchResults.Products[0].Name;

            Assert.True(productName == "xFold CINEMA X12 RTF U7");

            // now test filtering by outline
            var category = catalogRepo.Categories.SingleOrDefault(x => x.Name.Equals("Cell phones", StringComparison.OrdinalIgnoreCase));

            criteria = new ProductSearch
            {
                Outline = category.Id,
                Currency = "USD",
                Sort = new[] { "name" }
            };

            serviceCriteria = criteria.AsCriteria<CatalogItemSearchCriteria>(store.Id, store.Catalog, filters);
            searchResults = ibs.SearchItems(_scope, serviceCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);

            Assert.True(searchResults.TotalCount == 6, $"Expected 6, but found {searchResults.TotalCount}");
        }

        #region Performance Tests
        [Benchmark(InnerIterationCount = 10)]
        [Trait("Category", "performance")]
        public void Can_perf_web_search_products()
        {
            var provider = GetSearchProvider();

            provider.RemoveAll(_scope, "");
            var controller = GetSearchIndexController(provider);
            controller.RemoveIndex(_scope, CatalogItemSearchCriteria.DocType);
            controller.BuildIndex(_scope, CatalogItemSearchCriteria.DocType, x => { });

            // sleep for index to be commited
            Thread.Sleep(5000);

            var storeRepo = GetStoreRepository();
            var storeObject = storeRepo.Stores.SingleOrDefault(x => x.Name.Equals(_storeName, StringComparison.OrdinalIgnoreCase));
            var store = GetStoreService().GetById(storeObject.Id);

            // get catalog id by name
            var catalogRepo = GetCatalogRepository();
            var catalog = catalogRepo.Catalogs.SingleOrDefault(x => x.Name.Equals(_storeName, StringComparison.OrdinalIgnoreCase));

            // find all products in the category
            var criteria = new ProductSearch
            {
                Currency = "USD"
            };


            var context = new Dictionary<string, object>
            {
                { "Store", store },
            };

            var filterService = GetBrowseFilterService();
            var filters = filterService.GetFilters(context);
            var serviceCriteria = criteria.AsCriteria<CatalogItemSearchCriteria>(store.Id, store.Catalog, filters);
            var ibs = GetItemBrowsingService(provider);

            Benchmark.Iterate(() =>
            {
                var searchResults = ibs.SearchItems(_scope, serviceCriteria, Domain.Catalog.Model.ItemResponseGroup.ItemLarge);
            });
        }
        #endregion

        #region Private Helper Methods

        private static ItemBrowsingService GetItemBrowsingService(ISearchProvider provider)
        {
            var settings = GetSettingsManager();
            var service = new ItemBrowsingService(GetItemService(), provider, new FileSystemBlobProvider("", "http://samplesite.com"), settings);
            return service;
        }

        private static SearchIndexController GetSearchIndexController(ISearchProvider provider)
        {
            var settings = GetSettingsManager();
            return new SearchIndexController(settings, provider,
                new CatalogItemIndexBuilder(provider, GetSearchService(), GetItemService(), GetPricingService(), GetChangeLogService(), new FileSystemBlobProvider("", "http://samplesite.com"), settings),
                new CategoryIndexBuilder(provider, GetSearchService(), GetCategoryService(), GetChangeLogService(), new FileSystemBlobProvider("", "http://samplesite.com"), settings));
        }

        private static ICommerceService GetCommerceService()
        {
            return new CommerceServiceImpl(GetCommerceRepository);
        }

        private static ISettingsManager GetSettingsManager()
        {
            var mock = new Mock<ISettingsManager>();
            mock.Setup(s => s.GetModuleSettings("VirtoCommerce.Store")).Returns(new SettingEntry[] { });
            mock.Setup(s => s.GetValue("VirtoCommerce.SearchApi.UseFullObjectIndexStoring", true)).Returns(true);
            return mock.Object;
        }

        private static ICatalogSearchService GetSearchService()
        {
            return new CatalogSearchServiceImpl(GetCatalogRepository, GetItemService(), GetCatalogService(), GetCategoryService());
        }

        private static IOutlineService GetOutlineService()
        {
            return new OutlineService();
        }

        private static IPricingService GetPricingService()
        {
            var cacheManager = new Mock<ICacheManager<object>>();
            var log = new Mock<ILog>();
            log.Setup(l => l.Error(It.IsAny<Exception>())).Callback((object ex) =>
            {
                Trace.Write(ex.ToString());
            });

            return new PricingServiceImpl(GetPricingRepository, GetItemService(), log.Object, cacheManager.Object, null, null, null);
        }

        private static IBrowseFilterService GetBrowseFilterService()
        {
            return new BrowseFilterService();
        }

        private static IStoreService GetStoreService()
        {
            var settings = GetSettingsManager();
            var shippingService = Mock.Of<IShippingMethodsService>(s => s.GetAllShippingMethods() == new ShippingMethod[] { });
            var paymentService = Mock.Of<IPaymentMethodsService>(s => s.GetAllPaymentMethods() == new PaymentMethod[] { });
            var taxService = Mock.Of<ITaxService>(s => s.GetAllTaxProviders() == new TaxProvider[] { });
            var dpService = GetDynamicPropertyService();

            return new StoreServiceImpl(GetStoreRepository, GetCommerceService(), settings, dpService, shippingService, paymentService, taxService);
        }

        private static IDynamicPropertyService GetDynamicPropertyService()
        {
            var service = new DynamicPropertyService(GetPlatformRepository);
            return service;
        }

        private static ICategoryService GetCategoryService()
        {
            return new CategoryServiceImpl(GetCatalogRepository, GetCommerceService(), GetOutlineService(), GetCacheManager());
        }

        private static ICatalogService GetCatalogService()
        {
            return new CatalogServiceImpl(GetCatalogRepository, GetCommerceService(), GetCacheManager());
        }

        private static IItemService GetItemService()
        {
            return new ItemServiceImpl(GetCatalogRepository, GetCommerceService(), GetOutlineService(), GetCacheManager());
        }

        private static IChangeLogService GetChangeLogService()
        {
            return new ChangeLogService(GetPlatformRepository);
        }

        private static IStoreRepository GetStoreRepository()
        {
            var result = new StoreRepositoryImpl("VirtoCommerce", new EntityPrimaryKeyGeneratorInterceptor(), new AuditableInterceptor(null));
            return result;
        }

        private static IPlatformRepository GetPlatformRepository()
        {
            var result = new PlatformRepository("VirtoCommerce", new EntityPrimaryKeyGeneratorInterceptor(), new AuditableInterceptor(null));
            return result;
        }

        private static IPricingRepository GetPricingRepository()
        {
            var result = new PricingRepositoryImpl("VirtoCommerce", new EntityPrimaryKeyGeneratorInterceptor(), new AuditableInterceptor(null));
            return result;
        }

        private static ICatalogRepository GetCatalogRepository()
        {
            var result = new CatalogRepositoryImpl("VirtoCommerce", new EntityPrimaryKeyGeneratorInterceptor(), new AuditableInterceptor(null));
            return result;
        }

        private static ICacheManager<object> GetCacheManager()
        {
            return new Mock<ICacheManager<object>>().Object;
        }

        private static IСommerceRepository GetCommerceRepository()
        {
            var result = new CommerceRepositoryImpl("VirtoCommerce", new EntityPrimaryKeyGeneratorInterceptor(), new AuditableInterceptor(null));
            return result;
        }
        #endregion
    }
}
