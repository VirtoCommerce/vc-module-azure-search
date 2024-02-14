using System;
using Azure.Search.Documents;
using Microsoft.Azure.Search.Models;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchRequest
    {
        public string AggregationId { get; set; }
        public string SearchText { get; set; }

        [Obsolete("Use SearchOptions", DiagnosticId = "VC0008", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions/")]
        public SearchParameters SearchParameters { get; set; }

        public SearchOptions SearchOptions { get; set; }
    }
}
