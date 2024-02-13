using System.Collections.Generic;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public interface IAzureSearchDocumentsRequestBuilder
    {
        IList<AzureSearchRequest> BuildRequest(SearchRequest request, string indexName, string documentType, IList<SearchField> availableFields, SearchQueryType queryParserType);
    }
}
