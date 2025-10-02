using Azure;
using Azure.Search.Documents.Models;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchResult
    {
        public string AggregationId { get; set; }

        public Response<SearchResults<SearchDocument>> SearchDocumentResponse { get; set; }
    }
}
