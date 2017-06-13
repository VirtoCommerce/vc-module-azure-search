using System;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.CoreModule.Search.Tests;
using VirtoCommerce.Domain.Search;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Tests
{
    [Trait("Category", "CI")]
    public class AzureSearchTests : SearchProviderTests
    {
        protected override ISearchProvider GetSearchProvider()
        {
            var serviceName = Environment.GetEnvironmentVariable("TestAzureSearchServiceName");
            var accessKey = Environment.GetEnvironmentVariable("TestAzureSearchAccessKey");

            var provider = new AzureSearchProvider(new SearchConnection($"server={serviceName};key={accessKey};scope=test"));
            return provider;
        }
    }
}
