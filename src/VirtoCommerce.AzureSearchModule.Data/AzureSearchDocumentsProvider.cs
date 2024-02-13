using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using VirtoCommerce.AzureSearchModule.Data.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using IndexingResult = VirtoCommerce.SearchModule.Core.Model.IndexingResult;
using SearchDocument = Azure.Search.Documents.Models.SearchDocument;
using SearchOptions = VirtoCommerce.SearchModule.Core.Model.SearchOptions;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchDocumentsProvider : ISearchProvider, ISupportPartialUpdate, ISupportSuggestions
    {
        public const string ContentAnalyzerName = "content_analyzer";

        /// <summary>
        /// Name of the default suggester
        /// </summary>
        protected const string SuggesterName = "default_suggester";
        protected const string SuggestFieldSuffix = "__suggest";
        private const int MillisecondsDelay = 1000;
        private readonly ConcurrentDictionary<string, IList<SearchField>> _mappings = new();

        private readonly AzureSearchOptions _azureSearchOptions;
        private readonly SearchOptions _searchOptions;
        private readonly ISettingsManager _settingsManager;
        private readonly IAzureSearchDocumentsRequestBuilder _requestBuilder;
        private readonly IAzureSearchDocumentsResponseBuilder _responseBuilder;

        public AzureSearchDocumentsProvider(
            IOptions<AzureSearchOptions> azureSearchOptions,
            IOptions<SearchOptions> searchOptions,
            ISettingsManager settingsManager,
            IAzureSearchDocumentsRequestBuilder requestBuilder,
            IAzureSearchDocumentsResponseBuilder responseBuilder)
        {
            _azureSearchOptions = azureSearchOptions.Value;
            _searchOptions = searchOptions.Value;
            _settingsManager = settingsManager;
            _requestBuilder = requestBuilder;
            _responseBuilder = responseBuilder;
            _azureKeyCredential = new AzureKeyCredential(_azureSearchOptions.Key);
        }

        private readonly AzureKeyCredential _azureKeyCredential;
        private SearchIndexClient _client;
        protected SearchIndexClient Client => _client ??= GetSearchIndexClient();

        public virtual async Task DeleteIndexAsync(string documentType)
        {
            ArgumentNullException.ThrowIfNull(documentType);

            try
            {
                var indexName = GetIndexName(documentType);

                if (await IndexExistsAsync(indexName))
                {
                    await Client.DeleteIndexAsync(indexName);
                }

                RemoveMappingFromCache(indexName);
            }
            catch (RequestFailedException ex)
            {
                ThrowException("Failed to delete index", ex);
            }
        }

        public virtual async Task<IndexingResult> IndexAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = await InternalIndexAsync(documentType, documents, new IndexingParameters());

            return result;
        }

        public virtual async Task<IndexingResult> IndexPartialAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = await InternalIndexAsync(documentType, documents, new IndexingParameters { PartialUpdate = true });

            return result;
        }

        public virtual async Task<IndexingResult> RemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            IndexingResult result;

            try
            {
                var indexName = GetIndexName(documentType);
                var providerDocuments = documents.Select(document => ConvertToProviderDocument(document, null, documentType)).ToList();

                var batch = IndexDocumentsBatch.Delete(providerDocuments);

                var indexClient = GetSearchIndexClient(indexName);
                var response = await indexClient.IndexDocumentsAsync(batch);

                result = CreateIndexingResult(response.Value.Results);
            }
            catch (RequestFailedException ex)
            {
                throw new SearchException(ex.Message, ex);
            }

            return result;
        }

        public virtual async Task<SearchResponse> SearchAsync(string documentType, SearchRequest request)
        {
            var indexName = GetIndexName(documentType);

            try
            {
                var availableFields = await GetMappingAsync(indexName);

                var result = new SearchResponse();
                if (availableFields.IsNullOrEmpty())
                {
                    return result;
                }

                var searchClient = GetSearchIndexClient(indexName);

                var providerRequests = _requestBuilder.BuildRequest(request, indexName, documentType, availableFields, _azureSearchOptions.QueryParserType);
                var providerResponses = await Task.WhenAll(providerRequests.Select(r => searchClient.SearchAsync<SearchDocument>(r.SearchText, r.SearchOptions)));

                // Copy aggregation ID from request to response
                var searchResults = providerResponses.Select((response, i) =>
                {
                    return new AzureSearchResult
                    {
                        AggregationId = providerRequests[i].AggregationId,
                        SearchDocumentResponse = response,
                    };
                })
                .ToArray();

                result = _responseBuilder.ToSearchResponse(searchResults, request, documentType);

                return result;
            }
            catch (Exception ex)
            {
                throw new SearchException(ex.Message, ex);
            }
        }

        public async Task<SuggestionResponse> GetSuggestionsAsync(string documentType, SuggestionRequest request)
        {
            var result = new SuggestionResponse();

            if (request.Fields.IsNullOrEmpty())
            {
                return result;
            }

            try
            {
                var indexName = GetIndexName(documentType);

                var indexClient = GetSearchIndexClient(indexName);

                var suggestParameters = new SuggestOptions
                {
                    Size = request.Size,
                };

                var suggestResult = await indexClient.SuggestAsync<SearchDocument>(request.Query, SuggesterName, suggestParameters);

                result.Suggestions = suggestResult.Value.Results.Select(x => x.Text).ToList();
            }
            catch (RequestFailedException ex)
            {
                throw new SearchException(ex.Message, ex);
            }

            return result;
        }

        protected virtual async Task<IndexingResult> InternalIndexAsync(string documentType, IList<IndexDocument> documents, IndexingParameters parameters)
        {
            var indexName = GetIndexName(documentType);

            var providerFields = await GetMappingAsync(indexName);
            var oldFieldsCount = providerFields.Count;

            var providerDocuments = documents.Select(document => ConvertToProviderDocument(document, providerFields, documentType)).ToList();

            var updateMapping = !parameters.PartialUpdate && providerFields.Count != oldFieldsCount;

            var indexExits = await IndexExistsAsync(indexName);

            if (!indexExits)
            {
                try
                {
                    await CreateIndex(indexName, providerFields);
                }
                catch (RequestFailedException exception)
                {
                    ThrowException(exception, providerFields: providerFields);
                }
            }

            if (updateMapping)
            {
                await UpdateMapping(indexName, providerFields);
            }

            var result = await IndexWithRetryAsync(indexName, providerDocuments, 10, parameters.PartialUpdate);
            return result;
        }

        protected virtual SearchDocument ConvertToProviderDocument(IndexDocument document, IList<SearchField> providerFields, string documentType)
        {
            var result = new Dictionary<string, object>();

            if (document.Fields.All(x => x.Name != AzureSearchHelper.RawKeyFieldName))
            {
                document.Fields.Insert(0, new IndexDocumentField(AzureSearchHelper.RawKeyFieldName, document.Id, IndexDocumentFieldValueType.String) { IsRetrievable = true, IsFilterable = true });
            }

            foreach (var field in document.Fields.OrderBy(f => f.Name))
            {
                var fieldName = AzureSearchHelper.ToAzureFieldName(field.Name);

                if (result.TryGetValue(fieldName, out var currentValue))
                {
                    var newValues = new List<object>();

                    if (currentValue is object[] currentValues)
                    {
                        newValues.AddRange(currentValues);
                    }
                    else
                    {
                        newValues.Add(currentValue);
                    }

                    newValues.AddRange(field.Values);
                    result[fieldName] = newValues.ToArray();
                }
                else
                {
                    var providerFieldType = GetProviderFieldType(documentType, field.Name, field);

                    var isGeoPoint = providerFieldType == SearchFieldDataType.GeographyPoint;
                    var isComplex = providerFieldType == SearchFieldDataType.Complex;
                    providerFieldType = isComplex ? SearchFieldDataType.String : providerFieldType; // transform DataType from complex to string

                    var providerField = AddProviderField(documentType, providerFields, fieldName, field, providerFieldType);
                    var isCollection = providerField.IsCollection();

                    if (!FieldTypeMatch(providerFieldType, providerField.Type))
                    {
                        var message = $"Field type mismatch. Document type: {documentType}. Document Id: {document.Id}. Field name: {field.Name}. Document field type: {providerFieldType}. Schema field type: {providerField.Type}";
                        ThrowException(message, null);
                    }

                    var value = GetFieldValue(field, isGeoPoint, isComplex, isCollection);
                    result.Add(fieldName, value);
                }
            }

            return new SearchDocument(result);
        }

        private static bool FieldTypeMatch(SearchFieldDataType documentFieldType, SearchFieldDataType schemaFieldType)
        {
            // integer literal can be converted to double in schema
            if ((documentFieldType == SearchFieldDataType.Int32 || documentFieldType == SearchFieldDataType.Int64) && schemaFieldType == SearchFieldDataType.Double)
            {
                return true;
            }

            return documentFieldType == schemaFieldType || SearchFieldDataType.Collection(documentFieldType) == schemaFieldType;
        }

        private static object GetFieldValue(IndexDocumentField field, bool isGeoPoint, bool isComplex, bool isCollection)
        {
            object value;

            if (isGeoPoint)
            {
                value = isCollection
                    ? field.Values.OfType<GeoPoint>().Select(x => x.ToDocumentValue()).ToArray()
                    : ((field.Value as GeoPoint)?.ToDocumentValue());
            }
            else if (isComplex)
            {
                value = isCollection
                    ? field.Values.Select(x => x.SerializeJson() as object).ToArray()
                    : field.Value.SerializeJson();
            }
            else
            {
                value = isCollection ? field.Values : field.Value;
            }

            return value;
        }

        protected virtual SearchField AddProviderField(string documentType, IList<SearchField> providerFields, string fieldName, IndexDocumentField field, SearchFieldDataType providerFieldType)
        {
            var providerField = providerFields?.FirstOrDefault(f => f.Name == fieldName);

            if (providerField == null)
            {
                providerField = CreateProviderField(documentType, fieldName, field, providerFieldType);
                providerFields?.Add(providerField);

                // create a duplicate field for suggestions only
                if (field.IsSuggestable)
                {
                    var suggestField = CreateProviderField(documentType, $"{fieldName}{SuggestFieldSuffix}", field, providerFieldType);
                    providerFields?.Add(suggestField);
                }
            }

            return providerField;
        }

        protected virtual SearchField CreateProviderField(string documentType, string fieldName, IndexDocumentField field, SearchFieldDataType providerFieldType)
        {
            var isGeoPoint = providerFieldType == SearchFieldDataType.GeographyPoint;
            var isString = providerFieldType == SearchFieldDataType.String;
            var isCollection = field.IsCollection && isString;

            if (isCollection)
            {
                providerFieldType = SearchFieldDataType.Collection(providerFieldType);
            }

            var providerField = new SearchField(fieldName, providerFieldType)
            {
                IsKey = fieldName == AzureSearchHelper.KeyFieldName,
                IsHidden = !field.IsRetrievable,
                IsSearchable = isString && field.IsSearchable,
                IsFilterable = field.IsFilterable,
                IsFacetable = field.IsFilterable && !isGeoPoint,
                IsSortable = field.IsFilterable && !isCollection,
            };

            if (providerField.IsSearchable == true)
            {
                providerField.IndexAnalyzerName = ContentAnalyzerName;
                providerField.SearchAnalyzerName = LexicalAnalyzerName.StandardLucene;
            }

            return providerField;
        }

        private SearchFieldDataType GetProviderFieldType(string documentType, string fieldName, IndexDocumentField field)
        {
            SearchFieldDataType providerFieldType;
            if (field.ValueType == IndexDocumentFieldValueType.Undefined)
            {
                var originalFieldType = field.Value?.GetType() ?? typeof(object);
                providerFieldType = GetProviderFieldType(documentType, fieldName, originalFieldType);
            }
            else
            {
                providerFieldType = GetProviderFieldType(documentType, fieldName, field.ValueType);
            }

            return providerFieldType;
        }

        protected virtual SearchFieldDataType GetProviderFieldType(string documentType, string fieldName, Type fieldType)
        {
            if (fieldType == typeof(string))
            {
                return SearchFieldDataType.String;
            }

            if (fieldType == typeof(int))
            {
                return SearchFieldDataType.Int32;
            }

            if (fieldType == typeof(long))
            {
                return SearchFieldDataType.Int64;
            }

            if (fieldType == typeof(double) || fieldType == typeof(decimal))
            {
                return SearchFieldDataType.Double;
            }

            if (fieldType == typeof(bool))
            {
                return SearchFieldDataType.Boolean;
            }

            if (fieldType == typeof(DateTimeOffset) || fieldType == typeof(DateTime))
            {
                return SearchFieldDataType.DateTimeOffset;
            }

            if (fieldType == typeof(GeoPoint))
            {
                return SearchFieldDataType.GeographyPoint;
            }

            throw new ArgumentException($"Field {fieldName} has unsupported type {fieldType}", nameof(fieldType));
        }

        protected virtual SearchFieldDataType GetProviderFieldType(string documentType, string fieldName, IndexDocumentFieldValueType fieldType)
        {
            switch (fieldType)
            {
                case IndexDocumentFieldValueType.Char:
                case IndexDocumentFieldValueType.String:
                    return SearchFieldDataType.String;
                case IndexDocumentFieldValueType.Byte:
                case IndexDocumentFieldValueType.Short:
                case IndexDocumentFieldValueType.Integer:
                    return SearchFieldDataType.Int32;
                case IndexDocumentFieldValueType.Long:
                    return SearchFieldDataType.Int64;
                case IndexDocumentFieldValueType.Double:
                case IndexDocumentFieldValueType.Decimal:
                    return SearchFieldDataType.Double;
                case IndexDocumentFieldValueType.Boolean:
                    return SearchFieldDataType.Boolean;
                case IndexDocumentFieldValueType.DateTime:
                    return SearchFieldDataType.DateTimeOffset;
                case IndexDocumentFieldValueType.GeoPoint:
                    return SearchFieldDataType.GeographyPoint;
                case IndexDocumentFieldValueType.Complex:
                    return SearchFieldDataType.Complex;
                default:
                    throw new ArgumentException($"Field {fieldName} has unsupported type {fieldType}", nameof(fieldType));
            }
        }

        protected virtual async Task<IndexingResult> IndexWithRetryAsync(string indexName, List<SearchDocument> providerDocuments, int retryCount, bool partialUpdate)
        {
            if (providerDocuments.Count == 0)
            {
                return new IndexingResult { Items = Array.Empty<IndexingResultItem>() };
            }

            IndexingResult result = null;

            var batch = partialUpdate ? IndexDocumentsBatch.MergeOrUpload(providerDocuments) : IndexDocumentsBatch.Upload(providerDocuments);

            var indexClient = GetSearchIndexClient(indexName);

            // Retry if cannot index documents after updating the mapping
            for (var i = retryCount - 1; i >= 0; i--)
            {
                try
                {
                    var response = await indexClient.IndexDocumentsAsync(batch);
                    result = CreateIndexingResult(response.Value.Results);
                    break;
                }
                catch (RequestFailedException exception)
                    when (i > 0 && exception.Message.Contains("Make sure to only use property names that are defined by the type"))
                {
                    // Need to wait some time until new mapping is applied
                    await Task.Delay(MillisecondsDelay);
                }
                catch (RequestFailedException exception)
                {
                    ThrowException(exception, providerDocuments: providerDocuments);
                }
            }

            return result;
        }

        protected virtual IndexingResult CreateIndexingResult(IEnumerable<Azure.Search.Documents.Models.IndexingResult> results)
        {
            return new IndexingResult
            {
                Items = results.Select(r => new IndexingResultItem
                {
                    Id = r.Key,
                    Succeeded = r.Succeeded,
                    ErrorMessage = r.ErrorMessage,
                }).ToArray(),
            };
        }

        protected virtual string GetIndexName(string documentType)
        {
            // Use different index for each document type
            return string.Join("-", _searchOptions.GetScope(documentType), documentType).ToLowerInvariant();
        }

        protected virtual async Task<bool> IndexExistsAsync(string indexName)
        {
            var result = false;

            try
            {
                await Client.GetIndexAsync(indexName);
                result = true;
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // index not found
            }

            return result;
        }

        protected virtual async Task<IList<SearchField>> GetIndexFields(string indexName)
        {
            var index = await Client.GetIndexAsync(indexName);
            return index.Value.Fields;
        }

        #region Create and configure index

        protected virtual Task CreateIndex(string indexName, IList<SearchField> providerFields)
        {
            var index = CreateIndexDefinition(indexName, providerFields);
            return Client.CreateIndexAsync(index);
        }

        protected virtual SearchIndex CreateIndexDefinition(string indexName, IList<SearchField> providerFields)
        {
            var minGram = _settingsManager.GetMinGram();
            var maxGram = _settingsManager.GetMaxGram();

            var index = new SearchIndex(indexName)
            {
                Fields = providerFields.OrderBy(f => f.Name).ToList()
            };

            index.TokenFilters.Add(new NGramTokenFilter(ModuleConstants.NGramFilterName) { MinGram = minGram, MaxGram = maxGram });
            index.TokenFilters.Add(new EdgeNGramTokenFilter(ModuleConstants.EdgeNGramFilterName) { MinGram = minGram, MaxGram = maxGram });

            var customAnalyzer = new CustomAnalyzer(ContentAnalyzerName, LexicalTokenizerName.Standard);
            customAnalyzer.TokenFilters.Add(TokenFilterName.Lowercase);
            customAnalyzer.TokenFilters.Add(new TokenFilterName(_settingsManager.GetTokenFilterName()));

            index.Analyzers.Add(customAnalyzer);

            // try adding suggesters
            var suggestSourceFields = new List<string>();
            foreach (var suggestField in providerFields.Where(x => x.Name.EndsWith(SuggestFieldSuffix)))
            {
                //take original field without the suffix
                var originalFieldName = suggestField.Name.Replace(SuggestFieldSuffix, string.Empty);
                var originalSuggestField = providerFields.FirstOrDefault(x => x.Name.EqualsInvariant(originalFieldName));
                if (originalSuggestField != null)
                {
                    suggestSourceFields.Add(originalSuggestField.Name);
                }
            }

            if (suggestSourceFields.Count > 0)
            {
                var suggester = new SearchSuggester(SuggesterName, suggestSourceFields);
                index.Suggesters.Add(suggester);
            }

            return index;
        }

        #endregion

        #region Mapping

        protected virtual async Task<IList<SearchField>> GetMappingAsync(string indexName)
        {
            var providerFields = GetMappingFromCache(indexName);
            if (providerFields.IsNullOrEmpty() && await IndexExistsAsync(indexName))
            {
                providerFields = await GetIndexFields(indexName);
            }

            providerFields ??= new List<SearchField>();
            AddMappingToCache(indexName, providerFields);

            return providerFields;
        }

        protected virtual async Task UpdateMapping(string indexName, IList<SearchField> providerFields)
        {
            var index = CreateIndexDefinition(indexName, providerFields);

            try
            {

                var updatedIndex = await Client.CreateOrUpdateIndexAsync(index);
                AddMappingToCache(indexName, updatedIndex.Value.Fields);
            }
            catch (RequestFailedException exception)
            {
                ThrowException(exception, providerFields: providerFields);
            }
        }

        protected virtual IList<SearchField> GetMappingFromCache(string indexName)
        {
            return _mappings.TryGetValue(indexName, out var mapping) ? mapping : new List<SearchField>();
        }

        protected virtual void AddMappingToCache(string indexName, IList<SearchField> providerFields)
        {
            _mappings[indexName] = providerFields;
        }

        protected virtual void RemoveMappingFromCache(string indexName)
        {
            _mappings.TryRemove(indexName, out _);
        }

        #endregion

        private void ThrowException(string message, Exception innerException)
        {
            throw new SearchException($"{message}. Search service name: {_azureSearchOptions.SearchServiceName}, Scope: {_searchOptions.Scope}", innerException);
        }

        private static void ThrowException(RequestFailedException exception, IList<SearchField> providerFields = null, IEnumerable<SearchDocument> providerDocuments = null)
        {
            var error = $"StatusCode: {exception.Status}; Content:{exception.Message}";

            if (providerDocuments != null)
            {
                var documentIds = string.Join(',', providerDocuments.Select(x => x.GetString(AzureSearchHelper.ToAzureFieldName("id"))));
                error = $"{error}; DocumentIds:{documentIds}";
            }

            if (providerFields != null)
            {
                var fieldNames = string.Join(',', providerFields.Select(x => x.Name));
                error = $"{error}; FieldNames:{fieldNames}";
            }

            throw new SearchException(error, exception);
        }

        protected virtual SearchClient GetSearchIndexClient(string indexName)
        {
            var client = Client.GetSearchClient(indexName);
            return client;
        }

        protected virtual SearchIndexClient GetSearchIndexClient()
        {
            var client = new SearchIndexClient(new Uri(_azureSearchOptions.Endpoint), _azureKeyCredential);
            return client;
        }
    }
}
