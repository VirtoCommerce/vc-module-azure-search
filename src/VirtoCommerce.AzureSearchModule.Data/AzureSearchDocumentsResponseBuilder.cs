using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Search.Documents.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using AzureSearchDocument = Azure.Search.Documents.Models.SearchDocument;
using FacetResults = System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<Azure.Search.Documents.Models.FacetResult>>;
using VirtoSearchDocument = VirtoCommerce.SearchModule.Core.Model.SearchDocument;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchDocumentsResponseBuilder : IAzureSearchDocumentsResponseBuilder
    {
        public SearchResponse ToSearchResponse(IList<AzureSearchResult> searchResults, SearchRequest request, string documentType)
        {
            var primaryResponse = searchResults.First().SearchDocumentResponse.Value;

            var result = new SearchResponse
            {
                TotalCount = primaryResponse.TotalCount ?? 0,
                Documents = primaryResponse.GetResults().Select(ToSearchDocument).ToList(),
                Aggregations = GetAggregations(searchResults, request),
            };


            return result;
        }

        public static VirtoSearchDocument ToSearchDocument(SearchResult<AzureSearchDocument> searchResult)
        {
            var result = new VirtoSearchDocument();

            foreach (var (docKey, docValue) in searchResult.Document)
            {
                var key = AzureSearchHelper.FromAzureFieldName(docKey);

                if (key.EqualsIgnoreCase(AzureSearchHelper.RawKeyFieldName))
                {
                    result.Id = docValue.ToStringInvariant();
                }
                else
                {
                    var value = docValue;

                    // Convert DateTimeOffset to DateTime
                    if (value is DateTimeOffset dateTimeOffset)
                    {
                        value = dateTimeOffset.UtcDateTime;
                    }

                    result[key] = value;
                }
            }

            result.SetRelevanceScore(searchResult.Score);

            return result;
        }

        private static List<AggregationResponse> GetAggregations(IList<AzureSearchResult> searchResults, SearchRequest request)
        {
            var result = new List<AggregationResponse>();

            // Combine facets from all responses to a single FacetResults
            var facetResults = searchResults
                .Select(x => x.SearchDocumentResponse)
                .Where(r => r.Value?.Facets != null)
                .SelectMany(r => r.Value.Facets)
                .ToList();

            if (facetResults.Count != 0)
            {
                var facets = new Dictionary<string, IList<FacetResult>>();
                foreach (var keyValuePair in facetResults)
                {
                    facets[keyValuePair.Key] = keyValuePair.Value;
                }

                var responses = request.Aggregations
                    .Select(a => GetAggregation(a, facets))
                    .Where(a => a != null && a.Values.Any());

                result.AddRange(responses);
            }

            // Add responses for aggregations with empty field name
            foreach (var searchResult in searchResults.Where(r => !string.IsNullOrEmpty(r.AggregationId) && r.SearchDocumentResponse.Value.TotalCount > 0))
            {
                result.Add(new AggregationResponse
                {
                    Id = searchResult.AggregationId,
                    Values = new List<AggregationResponseValue>
                        {
                            new()
                            {
                                Id = searchResult.AggregationId,
                                Count = searchResult.SearchDocumentResponse.Value.TotalCount ?? 0,
                            }
                        }
                });
            }

            return result;
        }

        private static AggregationResponse GetAggregation(AggregationRequest aggregationRequest, FacetResults facets)
        {
            AggregationResponse result = null;

            switch (aggregationRequest)
            {
                case TermAggregationRequest termAggregationRequest:
                    result = GetTermAggregation(termAggregationRequest, facets);
                    break;
                case RangeAggregationRequest rangeAggregationRequest:
                    result = GetRangeAggregation(rangeAggregationRequest, facets);
                    break;
            }

            return result;
        }

        private static AggregationResponse GetTermAggregation(TermAggregationRequest termAggregationRequest, FacetResults facets)
        {
            if (termAggregationRequest == null)
            {
                return null;
            }

            var azureFieldName = AzureSearchHelper.ToAzureFieldName(termAggregationRequest.FieldName);
            if (string.IsNullOrEmpty(azureFieldName))
            {
                return null;
            }

            var facetResults = facets.TryGetValue(azureFieldName, out var facet) ? facet : null;
            if (facetResults == null || facetResults.Count == 0)
            {
                return null;
            }

            var result = new AggregationResponse
            {
                Id = termAggregationRequest.Id ?? termAggregationRequest.FieldName,
                Values = new List<AggregationResponseValue>(),
            };

            var values = termAggregationRequest.Values;

            if (values != null)
            {
                foreach (var value in values)
                {
                    var facetResult = facetResults.FirstOrDefault(r => r.Value.ToStringInvariant().EqualsIgnoreCase(value));
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

            return result;
        }

        private static AggregationResponse GetRangeAggregation(RangeAggregationRequest rangeAggregationRequest, FacetResults facets)
        {
            if (rangeAggregationRequest == null)
            {
                return null;
            }

            var azureFieldName = AzureSearchHelper.ToAzureFieldName(rangeAggregationRequest.FieldName);
            if (string.IsNullOrEmpty(azureFieldName))
            {
                return null;
            }

            var facetResults = facets.TryGetValue(azureFieldName, out var facet) ? facet : null;
            if (facetResults == null || facetResults.Count == 0)
            {
                return null;
            }

            var result = new AggregationResponse
            {
                Id = (rangeAggregationRequest.Id ?? rangeAggregationRequest.FieldName).ToLowerInvariant(),
                Values = new List<AggregationResponseValue>(),
            };

            foreach (var value in rangeAggregationRequest.Values)
            {
                var facetResult = GetRangeFacetResult(value, facetResults);
                AddAggregationValue(result, facetResult, value.Id);
            }

            return result;
        }

        private static FacetResult GetRangeFacetResult(RangeAggregationRequestValue value, IEnumerable<FacetResult> facetResults)
        {
            var lower = string.IsNullOrEmpty(value.Lower) || value.Lower == "0"
                ? null
                : value.Lower;

            var upper = value.Upper;

            return facetResults.FirstOrDefault(r => r.Count > 0 && r.From?.ToStringInvariant() == lower && r.To?.ToStringInvariant() == upper);
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
