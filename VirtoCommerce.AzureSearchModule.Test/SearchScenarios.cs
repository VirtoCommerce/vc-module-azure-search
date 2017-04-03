using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using VirtoCommerce.SearchApiModule.Data.Model;
using VirtoCommerce.SearchModule.Core.Model.Filters;
using VirtoCommerce.SearchModule.Core.Model.Indexing;
using VirtoCommerce.SearchModule.Core.Model.Search;
using VirtoCommerce.SearchModule.Data.Providers.ElasticSearch.Nest;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Test
{
    [Collection("Search")]
    [Trait("Category", "CI")]
    public class SearchScenarios : SearchTestsBase
    {
        private const string _scope = "test";

        [Fact]
        public void Can_find_pricelists_prices()
        {
            var provider = GetSearchProvider();
            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new CatalogItemSearchCriteria
            {
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Currency = "usd",
                Pricelists = new[] { "default", "sale" }
            };

            var priceRangefilter = new PriceRangeFilter
            {
                Currency = "usd",
                Values = new[]
                {
                    new RangeFilterValue {Id = "0_to_100", Lower = "0", Upper = "100"},
                    new RangeFilterValue {Id = "100_to_700", Lower = "100", Upper = "700"}
                }
            };

            criteria.Add(priceRangefilter);

            var results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 6, $"Returns {results.DocCount} instead of 6");

            var priceCount = GetFacetCount(results, "Price", "0_to_100");
            Assert.True(priceCount == 2, $"Returns {priceCount} facets of 0_to_100 prices instead of 2");

            var priceCount2 = GetFacetCount(results, "Price", "100_to_700");
            Assert.True(priceCount2 == 3, $"Returns {priceCount2} facets of 100_to_700 prices instead of 3");

            criteria = new CatalogItemSearchCriteria
            {
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Currency = "usd",
                Pricelists = new[] { "sale", "default" }
            };

            criteria.Add(priceRangefilter);

            results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 6, $"\"Sample Product\" search returns {results.DocCount} instead of 6");

            var priceSaleCount = GetFacetCount(results, "Price", "0_to_100");
            Assert.True(priceSaleCount == 3, $"Returns {priceSaleCount} facets of 0_to_100 prices instead of 2");

            var priceSaleCount2 = GetFacetCount(results, "Price", "100_to_700");
            Assert.True(priceSaleCount2 == 2, $"Returns {priceSaleCount2} facets of 100_to_700 prices instead of 3");

        }

        [Fact]
        public void Throws_exceptions_elastic()
        {
            const string badscope = "doesntexist";
            const string baddocumenttype = "badtype";
            var provider = GetSearchProvider();

            // try removing non-existing index
            // no exception should be generated, since 404 will be just eaten when index doesn't exist
            provider.RemoveAll(badscope, "");
            provider.RemoveAll(badscope, baddocumenttype);

            // now create an index and try removing non-existent document type
            SearchHelper.CreateSampleIndex(provider, _scope);
            provider.RemoveAll(_scope, "sometype");

            var badProvider = GetBadSearchProvider();

            Assert.Throws<ElasticSearchException>(() => badProvider.RemoveAll(badscope, ""));

            var criteria = new CatalogItemSearchCriteria
            {
                SearchPhrase = "product",
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Pricelists = new string[] { }
            };

            Assert.Throws<ElasticSearchException>(() => badProvider.Search<DocumentDictionary>(_scope, criteria));
        }

        [Fact]
        public void Can_create_search_index()
        {
            var provider = GetSearchProvider();
            SearchHelper.CreateSampleIndex(provider, _scope);
        }

        [Fact]
        public void Can_find_items_by_id()
        {
            var provider = GetSearchProvider();
            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new CatalogItemSearchCriteria
            {
                Catalog = "goods",
                ProductIds = new[] { "red3", "another" },
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Pricelists = new string[] { },
            };

            var results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 2, $"Returns {results.DocCount} documents instead of 2");
            Assert.True(results.Documents.Any(d => (string)d.Id == "red3"), "Cannot find 'red3'");
            Assert.True(results.Documents.Any(d => (string)d.Id == "another"), "Cannot find 'another'");
        }

        [Fact]
        public void Can_find_item_using_search()
        {
            var provider = GetSearchProvider();
            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new CatalogItemSearchCriteria
            {
                SearchPhrase = "product",
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Pricelists = new string[] { },
                Sort = new SearchSort("somefield") // specifically add non-existent field
            };

            var results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 1, $"Returns {results.DocCount} instead of 1");

            criteria = new CatalogItemSearchCriteria
            {
                SearchPhrase = "sample product ",
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Pricelists = new string[] { }
            };

            results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 1, $"\"Sample Product\" search returns {results.DocCount} instead of 1");
        }

        [Fact]
        public void Can_sort_using_search()
        {
            var provider = GetSearchProvider();
            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new CatalogItemSearchCriteria
            {
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Pricelists = new string[] { },
                Sort = new SearchSort("name")
            };

            var results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 6, $"Returns {results.DocCount} instead of 1");
            var productName = results.Documents.ElementAt(0)["name"] as string; // black sox
            Assert.True(productName == "black sox");

            criteria = new CatalogItemSearchCriteria
            {
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Pricelists = new string[] { },
                Sort = new SearchSort("name", true)
            };

            results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 6, $"\"Sample Product\" search returns {results.DocCount} instead of 1");
            productName = results.Documents.ElementAt(0)["name"] as string; // sample product
            Assert.True(productName == "sample product");
        }

        [Fact]
        public void Can_get_item_facets()
        {
            var provider = GetSearchProvider();

            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new CatalogItemSearchCriteria
            {
                SearchPhrase = "",
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 0,
                StartingRecord = 0,
                Currency = "USD",
                Pricelists = new[] { "default" }
            };

            var filter = new AttributeFilter
            {
                Key = "Color",
                Values = new[]
                {
                    new AttributeFilterValue {Id = "red", Value = "red"},
                    new AttributeFilterValue {Id = "blue", Value = "blue"},
                    new AttributeFilterValue {Id = "black", Value = "black"}
                }
            };

            var rangefilter = new RangeFilter
            {
                Key = "size",
                Values = new[]
                {
                    new RangeFilterValue {Id = "0_to_5", Lower = "0", Upper = "5"},
                    new RangeFilterValue {Id = "5_to_10", Lower = "5", Upper = "10"}
                }
            };

            var priceRangefilter = new PriceRangeFilter
            {
                Currency = "usd",
                Values = new[]
                {
                    new RangeFilterValue {Id = "0_to_100", Lower = "0", Upper = "100"},
                    new RangeFilterValue {Id = "100_to_700", Lower = "100", Upper = "700"},
                    new RangeFilterValue {Id = "over_700", Lower = "700"},
                    new RangeFilterValue {Id = "under_100", Upper = "100"},
                }
            };

            criteria.Add(filter);
            criteria.Add(rangefilter);
            criteria.Add(priceRangefilter);

            var results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 0, $"Returns {results.DocCount} instead of 0");

            var redCount = GetFacetCount(results, "Color", "red");
            Assert.True(redCount == 3, $"Returns {redCount} facets of red instead of 3");

            var priceCount = GetFacetCount(results, "Price", "0_to_100");
            Assert.True(priceCount == 2, $"Returns {priceCount} facets of 0_to_100 prices instead of 2");

            var priceCount2 = GetFacetCount(results, "Price", "100_to_700");
            Assert.True(priceCount2 == 3, $"Returns {priceCount2} facets of 100_to_700 prices instead of 3");

            var priceCount3 = GetFacetCount(results, "Price", "over_700");
            Assert.True(priceCount3 == 1, $"Returns {priceCount3} facets of over_700 prices instead of 1");

            var priceCount4 = GetFacetCount(results, "Price", "under_100");
            Assert.True(priceCount4 == 2, $"Returns {priceCount4} facets of priceCount4 prices instead of 2");

            var sizeCount = GetFacetCount(results, "size", "0_to_5");
            Assert.True(sizeCount == 3, $"Returns {sizeCount} facets of 0_to_5 size instead of 3");

            var sizeCount2 = GetFacetCount(results, "size", "5_to_10");
            Assert.True(sizeCount2 == 1, $"Returns {sizeCount2} facets of 5_to_10 size instead of 1"); // only 1 result because upper bound is not included
        }

        [Fact]
        public void Can_get_item_outlines()
        {
            var provider = GetSearchProvider();

            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new CatalogItemSearchCriteria
            {
                SearchPhrase = "",
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 6,
                StartingRecord = 0,
                Currency = "USD",
                Pricelists = new[] { "default" }
            };

            var results = provider.Search<DocumentDictionary>(_scope, criteria);

            Assert.True(results.DocCount == 6, $"Returns {results.DocCount} instead of 6");

            int outlineCount;
            var outlineObject = results.Documents.ElementAt(0)["__outline"]; // can be JArray or object[] depending on provider used
            if (outlineObject is JArray)
                outlineCount = (outlineObject as JArray).Count;
            else
                outlineCount = ((object[])outlineObject).Length;

            Assert.True(outlineCount == 2, $"Returns {outlineCount} outlines instead of 2");
        }

        [Fact]
        public void Can_get_item_multiple_filters()
        {
            var provider = GetSearchProvider();
            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new CatalogItemSearchCriteria
            {
                SearchPhrase = "",
                IsFuzzySearch = true,
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                Currency = "USD",
                Pricelists = new[] { "default" }
            };

            var colorFilter = new AttributeFilter
            {
                Key = "Color",
                Values = new[]
                {
                    new AttributeFilterValue {Id = "red", Value = "red"},
                    new AttributeFilterValue {Id = "blue", Value = "blue"},
                    new AttributeFilterValue {Id = "black", Value = "black"}
                }
            };

            var filter = new AttributeFilter
            {
                Key = "Color",
                Values = new[]
                {
                    new AttributeFilterValue {Id = "black", Value = "black"}
                }
            };

            var rangefilter = new RangeFilter
            {
                Key = "size",
                Values = new[]
                {
                    new RangeFilterValue {Id = "0_to_5", Lower = "0", Upper = "5"},
                    new RangeFilterValue {Id = "5_to_10", Lower = "5", Upper = "11"}
                }
            };

            var priceRangefilter = new PriceRangeFilter
            {
                Currency = "usd",
                Values = new[]
                {
                    new RangeFilterValue {Id = "100_to_700", Lower = "100", Upper = "700"}
                }
            };

            criteria.Add(colorFilter);
            criteria.Add(rangefilter);
            criteria.Add(priceRangefilter);

            // add applied filters
            criteria.Apply(filter);
            criteria.Apply(rangefilter);
            criteria.Apply(priceRangefilter);

            var results = provider.Search<DocumentDictionary>(_scope, criteria);

            var blackCount = GetFacetCount(results, "Color", "black");
            Assert.True(blackCount == 1, $"Returns {blackCount} facets of black instead of 1");

            var redCount = GetFacetCount(results, "Color", "red");
            Assert.True(redCount == 2, $"Returns {redCount} facets of black instead of 2");

            var priceCount = GetFacetCount(results, "Price", "100_to_700");
            Assert.True(priceCount == 1, $"Returns {priceCount} facets of 100_to_700 instead of 1");

            Assert.True(results.DocCount == 1, $"Returns {results.DocCount} instead of 1");
        }

        [Fact]
        public void Can_find_using_simple_search()
        {
            var provider = GetSearchProvider();
            SearchHelper.CreateSampleIndex(provider, _scope);

            var criteria = new SimpleCatalogItemSearchCriteria
            {
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                RawQuery = "color:bLue"
            };

            var results = provider.Search<DocumentDictionary>(_scope, criteria);
            Assert.True(results.DocCount == 1, $"Returns {results.DocCount} instead of 1");
            var productName = results.Documents.ElementAt(0)["name"] as string; // black sox
            Assert.True(productName == "blue shirt");

            criteria = new SimpleCatalogItemSearchCriteria
            {
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                RawQuery = @"price_usd:[100 TO 199]"
            };

            results = provider.Search<DocumentDictionary>(_scope, criteria);
            Assert.True(results.DocCount == 1, $"Returns {results.DocCount} instead of 1");

            criteria = new SimpleCatalogItemSearchCriteria
            {
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                RawQuery = @"is:priced"
            };

            results = provider.Search<DocumentDictionary>(_scope, criteria);
            Assert.True(results.DocCount > 0, $"Returns {results.DocCount} instead of >0");

            criteria = new SimpleCatalogItemSearchCriteria
            {
                Catalog = "goods",
                RecordsToRetrieve = 10,
                StartingRecord = 0,
                RawQuery = @"is:visible is:red3"
            };

            results = provider.Search<DocumentDictionary>(_scope, criteria);
            Assert.True(results.DocCount == 1, $"Returns {results.DocCount} instead of 1");
        }


        private static int GetFacetCount(ISearchResults<DocumentDictionary> results, string fieldName, string facetKey)
        {
            if (results.Facets == null || results.Facets.Length == 0)
            {
                return 0;
            }

            var group = results.Facets.SingleOrDefault(fg => fg.FieldName.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

            return group?.Facets
                .Where(facet => facet.Key == facetKey)
                .Select(facet => facet.Count)
                .FirstOrDefault() ?? 0;
        }
    }
}
