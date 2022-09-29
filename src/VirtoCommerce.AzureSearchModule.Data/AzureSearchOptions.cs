using Microsoft.Azure.Search.Models;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchOptions
    {
        public string SearchServiceName { get; set; }
        public string Key { get; set; }
        /// <summary>
        /// Azure Cognitive Search supports two Lucene-based query languages: Simple and Full.
        /// </summary>
        /// <seealso cref="https://learn.microsoft.com/en-us/azure/search/query-simple-syntax"/>
        /// <seealso cref="https://learn.microsoft.com/en-us/azure/search/search-query-lucene-examples"/>
        public QueryType QueryParserType { get; set; } = QueryType.Simple;
    }
}
