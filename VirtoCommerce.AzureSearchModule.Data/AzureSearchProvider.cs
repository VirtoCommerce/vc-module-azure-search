using System;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Model.Search;
using VirtoCommerce.SearchModule.Core.Model.Search.Criterias;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchProvider : ISearchProvider
    {
        private readonly ISearchConnection _connection;

        public AzureSearchProvider(ISearchConnection connection, ISearchQueryBuilder[] queryBuilders)
        {
            _connection = connection;
            QueryBuilders = queryBuilders;
        }

        public ISearchQueryBuilder[] QueryBuilders { get; }

        public void Close(string scope, string documentType)
        {
            throw new NotImplementedException();
        }

        public void Commit(string scope)
        {
            throw new NotImplementedException();
        }

        public void Index<T>(string scope, string documentType, T document)
        {
            throw new NotImplementedException();
        }

        public int Remove(string scope, string documentType, string key, string value)
        {
            throw new NotImplementedException();
        }

        public bool RemoveAll(string scope, string documentType)
        {
            throw new NotImplementedException();
        }

        public ISearchResults<T> Search<T>(string scope, ISearchCriteria criteria)
            where T : class
        {
            throw new NotImplementedException();
        }
    }
}
