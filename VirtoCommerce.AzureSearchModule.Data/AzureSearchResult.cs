using System;
using Microsoft.Azure.Search.Models;

namespace VirtoCommerce.AzureSearchModule.Data
{
    [CLSCompliant(false)]
    public class AzureSearchResult
    {
        public string AggregationId { get; set; }
        public DocumentSearchResult<Document> ProviderResponse { get; set; }
    }
}
