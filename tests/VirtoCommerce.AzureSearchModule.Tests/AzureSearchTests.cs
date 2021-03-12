using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Tests
{
    [Trait("Category", "CI")]
    [Trait("Category", "IntegrationTest")]
    public class AzureSearchTests : SearchProviderTests
    {
        protected virtual IOptions<SearchOptions> GetSearchOptions()
        {
            return Options.Create(new SearchOptions { Scope = "test-core", Provider = "AzureSearch" });
        }

        protected virtual IOptions<AzureSearchOptions> GetAzureSearchOptions()
        {
            var searchServiceName = Environment.GetEnvironmentVariable("TestAzureSearchServiceName") ?? "Test SearchServiceName";
            var key = Environment.GetEnvironmentVariable("TestAzureSearchKey") ?? "Test key";

            return Options.Create(new AzureSearchOptions { SearchServiceName = searchServiceName, Key = key });
        }

        protected override ISearchProvider GetSearchProvider()
        {
            var azureSearchOptions = GetAzureSearchOptions();
            var options = GetSearchOptions();

            var provider = new AzureSearchProvider(azureSearchOptions, options, GetSettingsManager());
            return provider;
        }

        [Fact]
        public virtual async Task CheckCallIsIndexExists()
        {
            var provider = new MockAzureSearchProvider(GetAzureSearchOptions(), GetSearchOptions(), GetSettingsManager());

            Assert.Null(provider.CallGetMappingFromCache());

            await provider.CallGetMappingAsync();

            Assert.True(provider.IsIndexExistsAsyncCalled);
            Assert.NotNull(provider.CallGetMappingFromCache());
        }
    }
}
