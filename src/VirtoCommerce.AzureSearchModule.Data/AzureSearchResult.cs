using System;
using Azure;
using Azure.Search.Documents.Models;
using Microsoft.Azure.Search.Models;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchResult
    {
        public string AggregationId { get; set; }

        [Obsolete("Use SearchDocumentResponse", DiagnosticId = "VC0006", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions/")]
        public DocumentSearchResult<Document> ProviderResponse { get; set; }

        public Response<SearchResults<SearchDocument>> SearchDocumentResponse { get; set; }
    }
}
