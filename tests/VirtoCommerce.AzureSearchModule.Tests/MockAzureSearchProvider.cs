using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Options;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.AzureSearchModule.Tests
{
    public class MockAzureSearchProvider : AzureSearchDocumentsProvider
    {
        private const string _indexName = "TestIndex";

        public bool IsIndexExistsAsyncCalled { get; set; }

        public MockAzureSearchProvider(
            IOptions<AzureSearchOptions> azureSearchOptions,
            IOptions<SearchOptions> searchOptions,
            ISettingsManager settingsManager,
            IAzureSearchDocumentsRequestBuilder requestBuilder,
            IAzureSearchDocumentsResponseBuilder responseBuilder) :
            base(azureSearchOptions, searchOptions, settingsManager, requestBuilder, responseBuilder, null)
        {
            IsIndexExistsAsyncCalled = false;
        }

        protected override Task<IList<SearchField>> GetIndexFields(string indexName)
        {
            return Task.FromResult<IList<SearchField>>(new List<SearchField>());
        }

        protected override Task<bool> IndexExistsAsync(string indexName)
        {
            IsIndexExistsAsyncCalled = true;
            return Task.FromResult(true);
        }

        public Task<IList<SearchField>> CallGetMappingAsync()
        {
            return GetMappingAsync(_indexName);
        }

        public IList<SearchField> CallGetMappingFromCache()
        {
            return GetMappingFromCache(_indexName);
        }
    }
}
