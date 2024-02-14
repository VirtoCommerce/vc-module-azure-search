using System.Collections.Generic;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public interface IAzureSearchDocumentsResponseBuilder
    {
        SearchResponse ToSearchResponse(IList<AzureSearchResult> searchResults, SearchRequest request, string documentType);
    }
}
