using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Tests
{
    [Trait("Category", "CI")]
    [Trait("Category", "IntegrationTest")]
    public class AzureSearchTests : SearchProviderTests
    {
        private readonly IAzureSearchDocumentsRequestBuilder _requestBuilder = new AzureSearchDocumentsRequestBuilder();
        private readonly IAzureSearchDocumentsResponseBuilder _responseBuilder = new AzureSearchDocumentsResponseBuilder();

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

            var loggerFactory = LoggerFactory.Create(builder => { builder.ClearProviders(); });
            var logger = loggerFactory.CreateLogger<AzureSearchDocumentsProvider>();

            var provider = new AzureSearchDocumentsProvider(azureSearchOptions, options, GetSettingsManager(), _requestBuilder, _responseBuilder, logger);
            return provider;
        }

        [Fact]
        public virtual async Task CheckCallIsIndexExists()
        {
            var provider = new MockAzureSearchProvider(GetAzureSearchOptions(), GetSearchOptions(), GetSettingsManager(), _requestBuilder, _responseBuilder);

            Assert.Null(provider.CallGetMappingFromCache());

            await provider.CallGetMappingAsync();

            Assert.True(provider.IsIndexExistsAsyncCalled);
            Assert.NotNull(provider.CallGetMappingFromCache());
        }

        [Theory]
        [InlineData(SearchOperation.Remove, "test-core-member-active")]
        [InlineData(SearchOperation.Search, "test-core-member-active")]
        [InlineData(SearchOperation.SearchBackup, "test-core-member-backup")]
        [InlineData(SearchOperation.Index, "test-core-member-active")]
        [InlineData(SearchOperation.IndexPartial, "test-core-member-active")]
        [InlineData(SearchOperation.IndexWithBackup, "test-core-member-backup")]
        [InlineData(SearchOperation.Suggest, "test-core-member-active")]
        [InlineData(SearchOperation.SuggestBackup, "test-core-member-backup")]
        public virtual async Task Operations_ResolveExpectedIndexName(SearchOperation operation, string expectedIndexName)
        {
            // Arrange
            var provider = new MockAzureSearchProvider(GetAzureSearchOptions(), GetSearchOptions(), GetSettingsManager(), _requestBuilder, _responseBuilder);

            IList<IndexDocument> documents = [new("Item-1")];

            Func<Task> action = operation switch
            {
                SearchOperation.Remove => () => provider.RemoveAsync("Member", documents),
                SearchOperation.Search => () => provider.SearchAsync("Member", new SearchRequest()),
                SearchOperation.SearchBackup => () => provider.SearchAsync("Member", new SearchRequest { UseBackupIndex = true }),
                SearchOperation.Index => () => provider.IndexAsync("Member", documents),
                SearchOperation.IndexPartial => () => provider.IndexPartialAsync("Member", documents),
                SearchOperation.IndexWithBackup => () => provider.IndexWithBackupAsync("Member", documents),
                SearchOperation.Suggest => () => provider.GetSuggestionsAsync("Member", new SuggestionRequest { Fields = ["Name"] }),
                SearchOperation.SuggestBackup => () => provider.GetSuggestionsAsync("Member", new SuggestionRequest { Fields = ["Name"], UseBackupIndex = true }),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };

            // Act
            await Assert.ThrowsAsync<SearchException>(action);

            // Assert
            Assert.Equal(expectedIndexName, provider.CapturedIndexName);
        }

        public enum SearchOperation
        {
            Remove,
            Search,
            SearchBackup,
            Index,
            IndexPartial,
            IndexWithBackup,
            Suggest,
            SuggestBackup,
        }
    }
}
