using VirtoCommerce.AzureSearchModule.Data;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model.Search;

namespace VirtoCommerce.AzureSearchModule.Test
{
    public class SearchTestsBase
    {
        protected static ISearchProvider GetSearchProvider()
        {
            return CreateSearchProvider("", "test");
        }

        protected static ISearchProvider GetBadSearchProvider()
        {
            return CreateSearchProvider("", "test");
        }


        private static ISearchProvider CreateSearchProvider(string dataSource, string scope)
        {
            var connection = new SearchConnection(dataSource, scope);
            var queryBuilder = new AzureSearchQueryBuilder() as ISearchQueryBuilder;
            var provider = new AzureSearchProvider(connection, new[] { queryBuilder });

            return provider;
        }
    }
}
