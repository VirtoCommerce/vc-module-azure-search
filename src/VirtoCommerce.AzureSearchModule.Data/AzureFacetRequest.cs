namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureFacetRequest
    {
        public string Id { get; set; }
        public string FieldName { get; set; }
        public string Filter { get; set; }
        public string AggregationRequest { get; set; }
    }
}
