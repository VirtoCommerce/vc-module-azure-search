using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
