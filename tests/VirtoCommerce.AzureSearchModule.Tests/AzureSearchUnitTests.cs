using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using Moq;
using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Tests;

public class AzureSearchUnitTests
{
    private readonly IAzureSearchDocumentsRequestBuilder _requestBuilder = new AzureSearchDocumentsRequestBuilder();
    private readonly IAzureSearchDocumentsResponseBuilder _responseBuilder = new AzureSearchDocumentsResponseBuilder();

    [Fact]
    public virtual async Task CheckCallIsIndexExists()
    {
        var provider = GetMockAzureSearchProvider();

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
        var provider = GetMockAzureSearchProvider();

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

    [Fact]
    public void BuildRequest_WithIncludeFields_LimitsSelectToRequestedFieldsAndKey()
    {
        // Arrange: VP-9334 / VCST-6425 - honour SearchRequest.IncludeFields so the
        // provider asks Azure for only the requested fields instead of full documents.
        var request = new SearchRequest
        {
            IncludeFields = new List<string> { "Name", "Code" },
        };
        var availableFields = new List<SearchField>
        {
            new(AzureSearchHelper.KeyFieldName, SearchFieldDataType.String),
            new(AzureSearchHelper.ToAzureFieldName("Name"), SearchFieldDataType.String),
            new(AzureSearchHelper.ToAzureFieldName("Code"), SearchFieldDataType.String),
        };

        // Act
        var requests = _requestBuilder.BuildRequest(request, "test-index", "Product", availableFields, SearchQueryType.Simple);

        // Assert
        var select = requests[0].SearchOptions.Select;
        Assert.Contains(AzureSearchHelper.KeyFieldName, select); // key field is required to resolve Document.Id
        Assert.Contains(AzureSearchHelper.ToAzureFieldName("Name"), select);
        Assert.Contains(AzureSearchHelper.ToAzureFieldName("Code"), select);
    }

    [Fact]
    public void BuildRequest_WithIncludeFieldNotInIndex_ExcludesItFromSelect()
    {
        // VP-9334 regression guard: a requested field that is not in the index schema (e.g. the storefront's
        // virtual "__object_id") must NOT be added to $select, or Azure rejects the whole query with HTTP 400
        // "Could not find a property named 'f___object_id'".
        var request = new SearchRequest
        {
            IncludeFields = new List<string> { "Name", "__object_id" },
        };
        var availableFields = new List<SearchField>
        {
            new(AzureSearchHelper.KeyFieldName, SearchFieldDataType.String),
            new(AzureSearchHelper.ToAzureFieldName("Name"), SearchFieldDataType.String),
        };

        // Act
        var requests = _requestBuilder.BuildRequest(request, "test-index", "Product", availableFields, SearchQueryType.Simple);

        // Assert
        var select = requests[0].SearchOptions.Select;
        Assert.Contains(AzureSearchHelper.ToAzureFieldName("Name"), select);          // f_name exists -> selected
        Assert.DoesNotContain(AzureSearchHelper.ToAzureFieldName("__object_id"), select); // f___object_id not in index -> excluded
        Assert.Contains(AzureSearchHelper.KeyFieldName, select);                      // key always included
    }

    [Fact]
    public void BuildRequest_WithoutIncludeFields_LeavesSelectEmpty()
    {
        // Arrange: no IncludeFields -> preserve current behaviour (Azure returns all retrievable fields).
        var request = new SearchRequest();

        // Act
        var requests = _requestBuilder.BuildRequest(request, "test-index", "Product", new List<SearchField>(), SearchQueryType.Simple);

        // Assert
        Assert.Empty(requests[0].SearchOptions.Select);
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


    private MockAzureSearchProvider GetMockAzureSearchProvider()
    {
        return new MockAzureSearchProvider(GetAzureSearchOptions(), GetSearchOptions(), GetSettingsManager(), _requestBuilder, _responseBuilder);
    }

    private IOptions<AzureSearchOptions> GetAzureSearchOptions()
    {
        var searchServiceName = Environment.GetEnvironmentVariable("TestAzureSearchServiceName") ?? "Test SearchServiceName";
        var key = Environment.GetEnvironmentVariable("TestAzureSearchKey") ?? "Test key";

        return Options.Create(new AzureSearchOptions { SearchServiceName = searchServiceName, Key = key });
    }

    private IOptions<SearchOptions> GetSearchOptions()
    {
        return Options.Create(new SearchOptions { Scope = "test-core", Provider = "AzureSearch" });
    }

    private ISettingsManager GetSettingsManager()
    {
        var mock = new Mock<ISettingsManager>();

        mock.Setup(s => s.GetObjectSettingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.FromResult<ObjectSettingEntry>(null));

        return mock.Object;
    }
}
