using Azure.Search.Documents;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchRequest
    {
        public string AggregationId { get; set; }
        public string SearchText { get; set; }
        public SearchOptions SearchOptions { get; set; }
    }
}
