using System;
using System.Collections.Generic;
using System.Linq;
using Azure.Search.Documents.Models;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;
using FacetResults = System.Collections.Generic.IDictionary<string, System.Collections.Generic.IList<Azure.Search.Documents.Models.FacetResult>>;
using SearchDocument = VirtoCommerce.SearchModule.Core.Model.SearchDocument;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public interface IAzureSearchDocumentsResponseBuilder
    {
        SearchResponse ToSearchResponse(IList<AzureSearchResult> searchResults, SearchRequest request, string documentType);
    }

    public class AzureSearchDocumentsResponseBuilder : IAzureSearchDocumentsResponseBuilder
    {
        public SearchResponse ToSearchResponse(IList<AzureSearchResult> searchResults, SearchRequest request, string documentType)
        {
            var primaryResponse = searchResults.First().SearchDocumentResponse.Value;

            var result = new SearchResponse
            {
                TotalCount = primaryResponse.TotalCount ?? 0,
                Aggregations = GetAggregations(searchResults, request),
            };

            var documents = new List<SearchDocument>();
            foreach (var resultDocument in primaryResponse.GetResults())
            {
                var searchDocument = ToSearchDocument(resultDocument.Document);
                documents.Add(searchDocument);
            }

            result.Documents = documents;

            return result;
        }

        public static SearchDocument ToSearchDocument(Azure.Search.Documents.Models.SearchDocument searchResult)
        {
            var result = new SearchDocument();

            foreach (var (docKey, docValue) in searchResult)
            {
                var key = AzureSearchHelper.FromAzureFieldName(docKey);

                if (key.EqualsInvariant(AzureSearchHelper.RawKeyFieldName))
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

            return result;
        }

        private static List<AggregationResponse> GetAggregations(IList<AzureSearchResult> searchResults, SearchRequest request)
        {
            var result = new List<AggregationResponse>();

            // Combine facets from all responses to a single FacetResults
            var facetResults = searchResults.Select(x => x.SearchDocumentResponse).Where(r => r.Value?.Facets != null).SelectMany(r => r.Value.Facets).ToList();

            if (facetResults.Count != 0)
            {
                var facets = new Dictionary<string, IList<Azure.Search.Documents.Models.FacetResult>>();
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
            AggregationResponse result = null;

            if (termAggregationRequest != null)
            {
                var azureFieldName = AzureSearchHelper.ToAzureFieldName(termAggregationRequest.FieldName);
                if (!string.IsNullOrEmpty(azureFieldName))
                {
                    var facetResults = facets.TryGetValue(azureFieldName, out var facet) ? facet : null;

                    if (facetResults != null && facetResults.Any())
                    {
                        result = new AggregationResponse
                        {
                            Id = termAggregationRequest.Id ?? termAggregationRequest.FieldName,
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
            }

            return result;
        }

        private static AggregationResponse GetRangeAggregation(RangeAggregationRequest rangeAggregationRequest, FacetResults facets)
        {
            AggregationResponse result = null;

            if (rangeAggregationRequest != null)
            {
                var azureFieldName = AzureSearchHelper.ToAzureFieldName(rangeAggregationRequest.FieldName);
                if (!string.IsNullOrEmpty(azureFieldName))
                {
                    var facetResults = facets.TryGetValue(azureFieldName, out var facet) ? facet : null;

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
            }

            return result;
        }

        private static FacetResult GetRangeFacetResult(RangeAggregationRequestValue value, IEnumerable<FacetResult> facetResults)
        {
            var lower = value.Lower == null ? null : value.Lower.Length == 0 ? null : value.Lower == "0" ? null : value.Lower;
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
