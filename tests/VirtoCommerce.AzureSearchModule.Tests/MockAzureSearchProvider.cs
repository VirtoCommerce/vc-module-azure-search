using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.AzureSearchModule.Tests
{
    public class MockAzureSearchProvider : AzureSearchProvider
    {
        private const string _indexName = "TestIndex";

        public bool IsIndexExistsAsyncCalled { get; set; }

        public MockAzureSearchProvider(IOptions<AzureSearchOptions> azureSearchOptions, IOptions<SearchOptions> searchOptions, ISettingsManager settingsManager, IAzureSearchRequestBuilder requestBuilder) :
            base(azureSearchOptions, searchOptions, settingsManager, requestBuilder)
        {
            IsIndexExistsAsyncCalled = false;
        }

        protected override Task<IList<Field>> GetIndexFields(string indexName)
        {
            return Task.FromResult<IList<Field>>(new List<Field>());
        }

        protected override Task<bool> IndexExistsAsync(string indexName)
        {
            IsIndexExistsAsyncCalled = true;
            return Task.FromResult(true);
        }

        public Task<IList<Field>> CallGetMappingAsync()
        {
            return GetMappingAsync(_indexName);
        }

        public IList<Field> CallGetMappingFromCache()
        {
            return GetMappingFromCache(_indexName);
        }
    }
}
