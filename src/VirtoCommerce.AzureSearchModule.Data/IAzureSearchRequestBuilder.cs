using System.Collections.Generic;
using Microsoft.Azure.Search.Models;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public interface IAzureSearchRequestBuilder
    {
        IList<AzureSearchRequest> BuildRequest(SearchRequest request, string indexName, string documentType, IList<Field> availableFields, QueryType queryParserType = QueryType.Simple);
    }
}
