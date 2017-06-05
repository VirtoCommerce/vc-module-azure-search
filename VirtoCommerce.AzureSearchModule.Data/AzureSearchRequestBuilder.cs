using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Azure.Search.Models;
using VirtoCommerce.Domain.Search;

namespace VirtoCommerce.AzureSearchModule.Data
{
    [CLSCompliant(false)]
    public class AzureSearchRequestBuilder
    {
        public static AzureSearchRequest BuildRequest(SearchRequest request, string indexName, string documentType, IList<Field> availableFields)
        {
            var result = new AzureSearchRequest
            {
                SearchText = GetSearchText(request),
                SearchParameters = new SearchParameters
                {
                    QueryType = QueryType.Simple,
                    SearchMode = SearchMode.All,
                    IncludeTotalResultCount = true,
                    Filter = GetFilters(request, availableFields),
                    Facets = GetFacets(request, availableFields),
                    OrderBy = GetSorting(request, availableFields),
                    Skip = request.Skip,
                    Top = request.Take,
                }
            };

            return result;
        }


        private static string GetSearchText(SearchRequest request)
        {
            return request?.SearchKeywords;
        }

        private static IList<string> GetSorting(SearchRequest request, IList<Field> availableFields)
        {
            IList<string> result = null;

            if (request.Sorting != null)
            {
                var fields = request.Sorting
                    .Select(f => GetSortingField(f, availableFields))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();

                if (fields.Any())
                {
                    result = fields;
                }
            }

            return result;
        }

        private static string GetSortingField(SortingField sortingField, IList<Field> availableFields)
        {
            string result = null;

            var availableField = availableFields.Get(sortingField.FieldName);
            if (availableField != null)
            {
                var geoSorting = sortingField as GeoDistanceSortingField;

                var fieldName = geoSorting == null
                    ? availableField.Name
                    : AzureSearchHelper.GetGeoDistanceExpression(availableField.Name, geoSorting.Location);

                result = string.Join(" ", fieldName, sortingField.IsDescending ? "desc" : "asc");
            }

            return result;
        }

        private static string GetFilters(SearchRequest request, IList<Field> availableFields)
        {
            return GetFilterExpressionRecursive(request.Filter, availableFields);
        }

        private static string GetFilterExpressionRecursive(IFilter filter, IList<Field> availableFields)
        {
            string result = null;

            var idsFilter = filter as IdsFilter;
            var termFilter = filter as TermFilter;
            var rangeFilter = filter as RangeFilter;
            var geoDistanceFilter = filter as GeoDistanceFilter;
            var notFilter = filter as NotFilter;
            var andFilter = filter as AndFilter;
            var orFilter = filter as OrFilter;

            if (idsFilter != null)
            {
                result = CreateIdsFilter(idsFilter);
            }
            else if (termFilter != null)
            {
                result = CreateTermFilter(termFilter, availableFields);
            }
            else if (rangeFilter != null)
            {
                result = CreateRangeFilter(rangeFilter, availableFields);
            }
            else if (geoDistanceFilter != null)
            {
                result = CreateGeoDistanceFilter(geoDistanceFilter, availableFields);
            }
            else if (notFilter != null)
            {
                result = CreateNotFilter(notFilter, availableFields);
            }
            else if (andFilter != null)
            {
                result = CreateAndFilter(andFilter, availableFields);
            }
            else if (orFilter != null)
            {
                result = CreateOrFilter(orFilter, availableFields);
            }

            return result;
        }

        private static string CreateIdsFilter(IdsFilter idsFilter)
        {
            string result = null;

            if (idsFilter?.Values != null)
            {
                result = GetEqualsFilterExpression(AzureSearchHelper.RawKeyFieldName, idsFilter.Values, false);
            }

            return result;
        }

        private static string CreateTermFilter(TermFilter termFilter, IList<Field> availableFields)
        {
            string result;

            var availableField = availableFields.Get(termFilter.FieldName);
            if (availableField != null)
            {
                result = availableField.Type.ToString().StartsWith("Collection(")
                    ? GetContainsFilterExpression(termFilter.FieldName, termFilter.Values)
                    : GetEqualsFilterExpression(termFilter.FieldName, termFilter.Values, true);
            }
            else
            {
                result = AzureSearchHelper.NonExistentFieldFilter;
            }

            return result;
        }

        private static string CreateRangeFilter(RangeFilter rangeFilter, IList<Field> availableFields)
        {
            string result;

            var availableField = availableFields.Get(rangeFilter.FieldName);
            if (availableField != null)
            {
                var expressions = rangeFilter.Values
                    .Select(v => GetRangeFilterValueExpression(v, availableField.Name))
                    .Where(e => !string.IsNullOrEmpty(e))
                    .ToArray();

                result = AzureSearchHelper.JoinNonEmptyStrings(" or ", true, expressions);
            }
            else
            {
                result = AzureSearchHelper.NonExistentFieldFilter;
            }

            return result;
        }

        private static string CreateGeoDistanceFilter(GeoDistanceFilter geoDistanceFilter, IList<Field> availableFields)
        {
            string result;

            var availableField = availableFields.Get(geoDistanceFilter.FieldName);
            if (availableField != null)
            {
                var distance = AzureSearchHelper.GetGeoDistanceExpression(availableField.Name, geoDistanceFilter.Location);
                result = string.Format(CultureInfo.InvariantCulture, "{0} le {1}", distance, geoDistanceFilter.Distance);
            }
            else
            {
                result = AzureSearchHelper.NonExistentFieldFilter;
            }

            return result;
        }

        private static string CreateNotFilter(NotFilter notFilter, IList<Field> availableFields)
        {
            string result = null;

            var childExpression = GetFilterExpressionRecursive(notFilter.ChildFilter, availableFields);
            if (childExpression != null)
            {
                result = $"not ({childExpression})";
            }

            return result;
        }

        private static string CreateAndFilter(AndFilter andFilter, IList<Field> availableFields)
        {
            string result = null;

            if (andFilter.ChildFilters != null)
            {
                var childExpressions = andFilter.ChildFilters.Select(q => GetFilterExpressionRecursive(q, availableFields)).ToArray();
                result = AzureSearchHelper.JoinNonEmptyStrings(" and ", true, childExpressions);
            }

            return result;
        }

        private static string CreateOrFilter(OrFilter orFilter, IList<Field> availableFields)
        {
            string result = null;

            if (orFilter.ChildFilters != null)
            {
                var childExpressions = orFilter.ChildFilters.Select(q => GetFilterExpressionRecursive(q, availableFields)).ToArray();
                result = AzureSearchHelper.JoinNonEmptyStrings(" or ", true, childExpressions);
            }

            return result;
        }

        private static string GetRangeFilterValueExpression(RangeFilterValue filterValue, string azureFieldName)
        {
            var lowerCondition = filterValue.IncludeLower ? "ge" : "gt";
            var upperCondition = filterValue.IncludeUpper ? "le" : "lt";
            return GetRangeFilterExpression(azureFieldName, filterValue.Lower, lowerCondition, filterValue.Upper, upperCondition);
        }

        private static string GetRangeFilterExpression(string azureFieldName, string lowerBound, string lowerCondition, string upperBound, string upperCondition)
        {
            string result = null;

            if (lowerBound?.Length > 0 && lowerCondition?.Length > 0 || upperBound?.Length > 0 && upperCondition?.Length > 0)
            {
                var builder = new StringBuilder();

                if (lowerBound?.Length > 0)
                {
                    builder.Append($"{azureFieldName} {lowerCondition} {lowerBound}");

                    if (upperBound?.Length > 0)
                    {
                        builder.Append(" and ");
                    }
                }

                if (upperBound?.Length > 0)
                {
                    builder.Append($"{azureFieldName} {upperCondition} {upperBound}");
                }

                result = builder.ToString();
            }

            return result;
        }

        private static string GetEqualsFilterExpression(string rawName, IEnumerable<string> rawValues, bool parseValues)
        {
            var azureFieldName = AzureSearchHelper.ToAzureFieldName(rawName);
            var values = rawValues.Where(v => !string.IsNullOrEmpty(v)).Select(v => GetFilterValue(v, parseValues)).ToArray();
            return AzureSearchHelper.JoinNonEmptyStrings(" or ", true, values.Select(v => $"{azureFieldName} eq {v}").ToArray());
        }

        private static string GetContainsFilterExpression(string rawName, IEnumerable<string> rawValues)
        {
            var azureFieldName = AzureSearchHelper.ToAzureFieldName(rawName);
            var values = rawValues.Where(v => !string.IsNullOrEmpty(v)).Select(GetStringFilterValue).ToArray();
            return AzureSearchHelper.JoinNonEmptyStrings(" or ", true, values.Select(v => $"{azureFieldName}/any(v: v eq {v})").ToArray());
        }

        private static string GetFilterValue(string rawValue, bool parseValue)
        {
            string result = null;

            if (parseValue)
            {
                DateTime dateValue;
                long integerValue;
                double doubleValue;

                if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateValue))
                {
                    result = rawValue;
                }
                else if (long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out integerValue))
                {
                    result = rawValue;
                }
                else if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue))
                {
                    result = rawValue;
                }
            }

            return result ?? GetStringFilterValue(rawValue);
        }

        private static string GetStringFilterValue(string rawValue)
        {
            return $"'{rawValue.Replace("'", "''")}'";
        }

        private static IList<string> GetFacets(SearchRequest request, IList<Field> availableFields)
        {
            var result = new List<string>();

            if (request.Aggregations != null)
            {
                foreach (var aggregation in request.Aggregations)
                {
                    var termAggregationRequest = aggregation as TermAggregationRequest;
                    var rangeAggregationRequest = aggregation as RangeAggregationRequest;

                    string facet = null;

                    if (termAggregationRequest != null)
                    {
                        facet = CreateTermAggregationRequest(termAggregationRequest, availableFields);
                    }
                    else if (rangeAggregationRequest != null)
                    {
                        facet = CreateRangeAggregationRequest(rangeAggregationRequest, availableFields);
                    }

                    if (!string.IsNullOrEmpty(facet))
                    {
                        result.Add(facet);
                    }
                }
            }

            return result;
        }

        private static string CreateTermAggregationRequest(TermAggregationRequest termAggregationRequest, IList<Field> availableFields)
        {
            string result = null;

            var availableField = availableFields.Get(termAggregationRequest.FieldName);
            if (availableField != null)
            {
                var builder = new StringBuilder(availableField.Name);

                if (termAggregationRequest.Size != null)
                {
                    builder.AppendFormat(CultureInfo.InvariantCulture, ",count:{0}", termAggregationRequest.Size);
                }

                result = builder.ToString();
            }

            return result;
        }

        private static string CreateRangeAggregationRequest(RangeAggregationRequest rangeAggregationRequest, IList<Field> availableFields)
        {
            string result = null;

            var availableField = availableFields.Get(rangeAggregationRequest.FieldName);
            if (availableField != null)
            {
                var edgeValues = rangeAggregationRequest.Values
                    .SelectMany(v => new[] { ConvertToDecimal(v.Lower), ConvertToDecimal(v.Upper) })
                    .Where(v => v > 0m)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToArray();

                var values = string.Join("|", edgeValues);

                result = $"{availableField.Name},values:{values}";
            }

            return result;
        }

        private static decimal? ConvertToDecimal(string input)
        {
            decimal? result = null;

            decimal value;
            if (decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                result = value;
            }

            return result;
        }
    }
}
