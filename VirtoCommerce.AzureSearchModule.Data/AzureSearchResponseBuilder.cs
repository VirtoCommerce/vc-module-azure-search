using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Search.Models;
using VirtoCommerce.Domain.Search;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.AzureSearchModule.Data
{
    [CLSCompliant(false)]
    public static class AzureSearchResponseBuilder
    {
        public static SearchResponse ToSearchResponse(this DocumentSearchResult response, SearchRequest request, string documentType)
        {
            var result = new SearchResponse
            {
                TotalCount = response.Count ?? 0,
                Documents = response.Results.Select(ToSearchDocument).ToArray(),
                Aggregations = GetAggregations(response.Facets, request)
            };

            return result;
        }

        public static SearchDocument ToSearchDocument(SearchResult searchResult)
        {
            var result = new SearchDocument();

            foreach (var kvp in searchResult.Document)
            {
                var key = AzureSearchHelper.FromAzureFieldName(kvp.Key);

                if (key.EqualsInvariant(AzureSearchHelper.RawKeyFieldName))
                {
                    result.Id = kvp.Value.ToStringInvariant();
                }
                else
                {
                    result[key] = kvp.Value;
                }
            }

            return result;
        }


        private static IList<AggregationResponse> GetAggregations(FacetResults facets, SearchRequest request)
        {
            IList<AggregationResponse> result = null;

            if (facets != null)
            {
                result = request.Aggregations
                    .Select(a => GetAggregation(a, facets))
                    .Where(a => a != null && a.Values.Any())
                    .ToList();
            }

            return result;
        }

        private static AggregationResponse GetAggregation(AggregationRequest aggregationRequest, FacetResults facets)
        {
            AggregationResponse result = null;

            var termAggregationRequest = aggregationRequest as TermAggregationRequest;
            var rangeAggregationRequest = aggregationRequest as RangeAggregationRequest;

            if (termAggregationRequest != null)
            {
                result = GetTermAggregation(termAggregationRequest, facets);
            }
            else if (rangeAggregationRequest != null)
            {
                result = GetRangeAggregation(rangeAggregationRequest, facets);
            }

            return result;
        }

        private static AggregationResponse GetTermAggregation(TermAggregationRequest termAggregationRequest, FacetResults facets)
        {
            AggregationResponse result = null;

            if (termAggregationRequest != null)
            {
                var azureFieldName = AzureSearchHelper.ToAzureFieldName(termAggregationRequest.FieldName);
                var facetResults = facets.ContainsKey(azureFieldName) ? facets[azureFieldName] : null;

                if (facetResults != null && facetResults.Any())
                {
                    result = new AggregationResponse
                    {
                        Id = (termAggregationRequest.Id ?? termAggregationRequest.FieldName).ToLowerInvariant(),
                        Values = new List<AggregationResponseValue>(),
                    };

                    var values = termAggregationRequest.Values;

                    if (values != null)
                    {
                        foreach (var value in values)
                        {
                            var facetResult = facetResults.FirstOrDefault(r => r.Value.ToStringInvariant().EqualsInvariant(value));
                            AddAggregationValue(result, facetResult, value);
                        }
                    }
                    else
                    {
                        // Return all facet results if values are not defined
                        foreach (var facetResult in facetResults)
                        {
                            var aggregationValue = new AggregationResponseValue
                            {
                                Id = facetResult.Value.ToStringInvariant(),
                                Count = facetResult.Count ?? 0,
                            };
                            result.Values.Add(aggregationValue);
                        }
                    }
                }
            }

            return result;
        }

        private static AggregationResponse GetRangeAggregation(RangeAggregationRequest rangeAggregationRequest, FacetResults facets)
        {
            AggregationResponse result = null;

            if (rangeAggregationRequest != null)
            {
                var azureFieldName = AzureSearchHelper.ToAzureFieldName(rangeAggregationRequest.FieldName);
                var facetResults = facets.ContainsKey(azureFieldName) ? facets[azureFieldName] : null;

                if (facetResults != null && facetResults.Any())
                {
                    result = new AggregationResponse
                    {
                        Id = (rangeAggregationRequest.Id ?? rangeAggregationRequest.FieldName).ToLowerInvariant(),
                        Values = new List<AggregationResponseValue>(),
                    };

                    foreach (var value in rangeAggregationRequest.Values)
                    {
                        var facetResult = GetRangeFacetResult(value, facetResults);
                        AddAggregationValue(result, facetResult, value.Id);
                    }
                }
            }

            return result;
        }

        private static FacetResult GetRangeFacetResult(RangeAggregationRequestValue value, IEnumerable<FacetResult> facetResults)
        {
            var lower = value.Lower == null ? null : value.Lower.Length == 0 ? null : value.Lower == "0" ? null : value.Lower;
            var upper = value.Upper;

            return facetResults.FirstOrDefault(r => r.Count > 0 && r.From?.ToString() == lower && r.To?.ToString() == upper);
        }

        private static void AddAggregationValue(AggregationResponse aggregation, FacetResult facetResult, string valueId)
        {
            if (facetResult != null && facetResult.Count > 0)
            {
                var aggregationValue = new AggregationResponseValue
                {
                    Id = valueId,
                    Count = facetResult.Count ?? 0,
                };
                aggregation.Values.Add(aggregationValue);
            }
        }
    }
}
