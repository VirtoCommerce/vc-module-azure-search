using System.Collections.Generic;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents.Indexes.Models;
using Microsoft.Extensions.Logging.Abstractions;
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

        public string CapturedIndexName { get; private set; }

        public MockAzureSearchProvider(
            IOptions<AzureSearchOptions> azureSearchOptions,
            IOptions<SearchOptions> searchOptions,
            ISettingsManager settingsManager,
            IAzureSearchDocumentsRequestBuilder requestBuilder,
            IAzureSearchDocumentsResponseBuilder responseBuilder) :
            base(azureSearchOptions, searchOptions, settingsManager, requestBuilder, responseBuilder, NullLogger<AzureSearchDocumentsProvider>.Instance)
        {
            IsIndexExistsAsyncCalled = false;
        }

        protected override Task<IList<SearchField>> GetIndexFields(string indexName)
        {
            return Task.FromResult<IList<SearchField>>([new SearchField("id", SearchFieldDataType.String)]);
        }

        protected override Task UpdateMapping(string indexName, IList<SearchField> providerFields)
        {
            return Task.CompletedTask;
        }

        protected override Task<bool> IndexExistsAsync(string indexName)
        {
            IsIndexExistsAsyncCalled = true;
            return Task.FromResult(true);
        }

        protected override Azure.Search.Documents.SearchClient GetSearchIndexClient(string indexName)
        {
            CapturedIndexName = indexName;

            // Short-circuit before the network call so the test stays offline.
            throw new RequestFailedException("Short-circuit before the network call in unit test");
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
