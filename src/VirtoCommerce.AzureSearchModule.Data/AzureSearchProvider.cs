using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Extensions.Options;
using Microsoft.Rest.Azure;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using DataType = Microsoft.Azure.Search.Models.DataType;
using Index = Microsoft.Azure.Search.Models.Index;
using IndexingResult = VirtoCommerce.SearchModule.Core.Model.IndexingResult;

namespace VirtoCommerce.AzureSearchModule.Data
{
    [Obsolete("Use AzureSearchDocumentsProvider", DiagnosticId = "VC0006", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions/")]
    public class AzureSearchProvider : ISearchProvider, ISupportPartialUpdate, ISupportSuggestions
    {
        public const string ContentAnalyzerName = "content_analyzer";

        [Obsolete("Use ModuleConstants.NGramFilterName", DiagnosticId = "VC0006", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions/")]
        public const string NGramFilterName = ModuleConstants.NGramFilterName;

        [Obsolete("Use ModuleConstants.EdgeNGramFilterName", DiagnosticId = "VC0006", UrlFormat = "https://docs.virtocommerce.org/products/products-virto3-versions/")]
        public const string EdgeNGramFilterName = ModuleConstants.EdgeNGramFilterName;

        /// <summary>
        /// Name of the default suggester
        /// </summary>
        protected const string SuggesterName = "default_suggester";
        protected const string SuggestFieldSuffix = "__suggest";

        private readonly AzureSearchOptions _azureSearchOptions;
        private readonly SearchOptions _searchOptions;
        private readonly ISettingsManager _settingsManager;
        private readonly ConcurrentDictionary<string, IList<Field>> _mappings = new();
        private readonly IAzureSearchRequestBuilder _requestBuilder;

        public AzureSearchProvider(IOptions<AzureSearchOptions> azureSearchOptions, IOptions<SearchOptions> searchOptions, ISettingsManager settingsManager, IAzureSearchRequestBuilder requestBuilder)
        {
            if (azureSearchOptions == null)
            {
                throw new ArgumentNullException(nameof(azureSearchOptions));
            }

            if (searchOptions == null)
            {
                throw new ArgumentNullException(nameof(searchOptions));
            }

            _azureSearchOptions = azureSearchOptions.Value;
            _searchOptions = searchOptions.Value;

            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _requestBuilder = requestBuilder;
        }

        private SearchServiceClient _client;
        protected SearchServiceClient Client => _client ??= CreateSearchServiceClient();

        public virtual async Task DeleteIndexAsync(string documentType)
        {
            if (string.IsNullOrEmpty(documentType))
            {
                throw new ArgumentNullException(nameof(documentType));
            }

            try
            {
                var indexName = GetIndexName(documentType);

                if (await IndexExistsAsync(indexName))
                {
                    await Client.Indexes.DeleteAsync(indexName);
                }

                RemoveMappingFromCache(indexName);
            }
            catch (Exception ex)
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
                var indexClient = GetSearchIndexClient(indexName);
                var providerDocuments = documents.Select(document => ConvertToProviderDocument(document, null, documentType)).ToList();
                var batch = IndexBatch.Delete(providerDocuments);

                var response = await indexClient.Documents.IndexAsync(batch);
                result = CreateIndexingResult(response.Results);
            }
            catch (IndexBatchException ex)
            {
                result = CreateIndexingResult(ex.IndexingResults);
            }
            catch (Exception ex)
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

                if (availableFields.IsNullOrEmpty())
                {
                    return new SearchResponse();
                }

                var indexClient = GetSearchIndexClient(indexName);

                var providerRequests = _requestBuilder.BuildRequest(request, indexName, documentType, availableFields, QueryType.Simple);
                var providerResponses = await Task.WhenAll(providerRequests.Select(r => indexClient.Documents.SearchAsync(r?.SearchText, r?.SearchParameters)));

                // Copy aggregation ID from request to response
                var searchResults = providerResponses.Select((response, i) => new AzureSearchResult
                {
                    AggregationId = providerRequests[i].AggregationId,
                    ProviderResponse = response,
                })
                .ToArray();

                var result = searchResults.ToSearchResponse(request, documentType);
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

                var suggestParameters = new SuggestParameters
                {
                    Top = request.Size,
                };

                var suggestResult = await indexClient.Documents.SuggestAsync(request.Query, SuggesterName, suggestParameters);

                result.Suggestions = suggestResult.Results.Select(x => x.Text).ToList();
            }
            catch (Exception ex)
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
                catch (CloudException cloudException)
                {
                    ThrowException(cloudException, providerFields: providerFields);
                }
            }

            if (updateMapping)
            {
                UpdateMapping(indexName, providerFields);
            }

            var result = await IndexWithRetryAsync(indexName, providerDocuments, 10, parameters.PartialUpdate);
            return result;
        }

        protected virtual SearchDocument ConvertToProviderDocument(IndexDocument document, IList<Field> providerFields, string documentType)
        {
            var result = new SearchDocument { Id = document.Id };

            if (document.Fields.All(x => x.Name != AzureSearchHelper.RawKeyFieldName))
            {
                document.Fields.Insert(0, new IndexDocumentField(AzureSearchHelper.RawKeyFieldName, document.Id, IndexDocumentFieldValueType.String) { IsRetrievable = true, IsFilterable = true });
            }

            foreach (var field in document.Fields.OrderBy(f => f.Name))
            {
                var fieldName = AzureSearchHelper.ToAzureFieldName(field.Name);

                if (result.ContainsKey(fieldName))
                {
                    var newValues = new List<object>();

                    var currentValue = result[fieldName];

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

                    var isGeoPoint = providerFieldType == DataType.GeographyPoint;
                    var isComplex = providerFieldType == DataType.Complex;
                    providerFieldType = isComplex ? DataType.String : providerFieldType; // transform DataType from complex to string

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

            return result;
        }

        private static bool FieldTypeMatch(DataType documentFieldType, DataType schemaFieldType)
        {
            // integer literal can be converted to double in schema
            if ((documentFieldType == DataType.Int32 || documentFieldType == DataType.Int64) && schemaFieldType == DataType.Double)
            {
                return true;
            }

            return documentFieldType == schemaFieldType || DataType.Collection(documentFieldType) == schemaFieldType;
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

        [Obsolete("Use AddProviderField(string documentType, IList<Field> providerFields, string fieldName, IndexDocumentField field, DataType providerFieldType)")]
        protected virtual Field AddProviderField(string documentType, IList<Field> providerFields, string fieldName, IndexDocumentField field)
        {
            var providerField = providerFields?.FirstOrDefault(f => f.Name == fieldName);

            if (providerField == null)
            {
                providerField = CreateProviderField(documentType, fieldName, field);
                providerFields?.Add(providerField);

                // create a duplicate field for suggestions only
                if (field.IsSuggestable)
                {
                    var suggestField = CreateProviderField(documentType, $"{fieldName}{SuggestFieldSuffix}", field);
                    providerFields?.Add(suggestField);
                }
            }

            return providerField;
        }

        protected virtual Field AddProviderField(string documentType, IList<Field> providerFields, string fieldName, IndexDocumentField field, DataType providerFieldType)
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

        [Obsolete("Use CreateProviderField(string documentType, string fieldName, IndexDocumentField field, DataType providerFieldType)")]
        protected virtual Field CreateProviderField(string documentType, string fieldName, IndexDocumentField field)
        {
            var providerFieldType = GetProviderFieldType(documentType, fieldName, field);
            return CreateProviderField(documentType, fieldName, field, providerFieldType);
        }

        protected virtual Field CreateProviderField(string documentType, string fieldName, IndexDocumentField field, DataType providerFieldType)
        {
            var isGeoPoint = providerFieldType == DataType.GeographyPoint;
            var isString = providerFieldType == DataType.String;
            var isCollection = field.IsCollection && isString;

            if (isCollection)
            {
                providerFieldType = DataType.Collection(providerFieldType);
            }

            var providerField = new Field(fieldName, providerFieldType)
            {
                IsKey = fieldName == AzureSearchHelper.KeyFieldName,
                IsRetrievable = field.IsRetrievable,
                IsSearchable = isString && field.IsSearchable,
                IsFilterable = field.IsFilterable,
                IsFacetable = field.IsFilterable && !isGeoPoint,
                IsSortable = field.IsFilterable && !isCollection,
            };

            if (providerField.IsSearchable == true)
            {
                providerField.IndexAnalyzer = ContentAnalyzerName;
                providerField.SearchAnalyzer = AnalyzerName.StandardLucene;
            }

            return providerField;
        }

        private DataType GetProviderFieldType(string documentType, string fieldName, IndexDocumentField field)
        {
            DataType providerFieldType;
            if (field.ValueType == IndexDocumentFieldValueType.Undefined)
            {
                var originalFieldType = field.Value?.GetType() ?? typeof(object);
#pragma warning disable CS0618 // Type or member is obsolete
                providerFieldType = GetProviderFieldType(documentType, fieldName, originalFieldType);
#pragma warning restore CS0618 // Type or member is obsolete
            }
            else
            {
                providerFieldType = GetProviderFieldType(documentType, fieldName, field.ValueType);
            }

            return providerFieldType;
        }

        [Obsolete("Left for backwards compatibility.")]
        protected virtual DataType GetProviderFieldType(string documentType, string fieldName, Type fieldType)
        {
            if (fieldType == typeof(string))
            {
                return DataType.String;
            }

            if (fieldType == typeof(int))
            {
                return DataType.Int32;
            }

            if (fieldType == typeof(long))
            {
                return DataType.Int64;
            }

            if (fieldType == typeof(double) || fieldType == typeof(decimal))
            {
                return DataType.Double;
            }

            if (fieldType == typeof(bool))
            {
                return DataType.Boolean;
            }

            if (fieldType == typeof(DateTimeOffset) || fieldType == typeof(DateTime))
            {
                return DataType.DateTimeOffset;
            }

            if (fieldType == typeof(GeoPoint))
            {
                return DataType.GeographyPoint;
            }

            throw new ArgumentException($"Field {fieldName} has unsupported type {fieldType}", nameof(fieldType));
        }

        protected virtual DataType GetProviderFieldType(string documentType, string fieldName, IndexDocumentFieldValueType fieldType)
        {
            switch (fieldType)
            {
                case IndexDocumentFieldValueType.Char:
                case IndexDocumentFieldValueType.String:
                    return DataType.String;
                case IndexDocumentFieldValueType.Byte:
                case IndexDocumentFieldValueType.Short:
                case IndexDocumentFieldValueType.Integer:
                    return DataType.Int32;
                case IndexDocumentFieldValueType.Long:
                    return DataType.Int64;
                case IndexDocumentFieldValueType.Double:
                case IndexDocumentFieldValueType.Decimal:
                    return DataType.Double;
                case IndexDocumentFieldValueType.Boolean:
                    return DataType.Boolean;
                case IndexDocumentFieldValueType.DateTime:
                    return DataType.DateTimeOffset;
                case IndexDocumentFieldValueType.GeoPoint:
                    return DataType.GeographyPoint;
                case IndexDocumentFieldValueType.Complex:
                    return DataType.Complex;
                default:
                    throw new ArgumentException($"Field {fieldName} has unsupported type {fieldType}", nameof(fieldType));
            }
        }

        protected virtual async Task<IndexingResult> IndexWithRetryAsync(string indexName, IEnumerable<SearchDocument> providerDocumentsEnumerable, int retryCount, bool partialUpdate)
        {
            // Avoid multiple enumeration. Need to change argument type to IList<SearchDocument>.
            var providerDocuments = providerDocumentsEnumerable as IList<SearchDocument> ?? providerDocumentsEnumerable.ToList();

            if (!providerDocuments.Any())
            {
                return new IndexingResult { Items = Array.Empty<IndexingResultItem>() };
            }

            IndexingResult result = null;

            var batch = partialUpdate ? IndexBatch.MergeOrUpload(providerDocuments) : IndexBatch.Upload(providerDocuments);
            var indexClient = GetSearchIndexClient(indexName);

            // Retry if cannot index documents after updating the mapping
            for (var i = retryCount - 1; i >= 0; i--)
            {
                try
                {
                    var response = await indexClient.Documents.IndexAsync(batch);
                    result = CreateIndexingResult(response.Results);
                    break;
                }
                catch (IndexBatchException ex)
                {
                    result = CreateIndexingResult(ex.IndexingResults);
                    break;
                }
                catch (CloudException cloudException)
                    when (i > 0 && cloudException.Message.Contains("Make sure to only use property names that are defined by the type"))
                {
                    // Need to wait some time until new mapping is applied
                    await Task.Delay(1000);
                }
                catch (CloudException cloudException)
                {
                    ThrowException(cloudException, providerDocuments: providerDocuments);
                }
            }

            return result;
        }

        protected virtual IndexingResult CreateIndexingResult(IEnumerable<Microsoft.Azure.Search.Models.IndexingResult> results)
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

        protected virtual Task<bool> IndexExistsAsync(string indexName)
        {
            return Client.Indexes.ExistsAsync(indexName);
        }

        protected virtual async Task<IList<Field>> GetIndexFields(string indexName)
        {
            var index = await Client.Indexes.GetAsync(indexName);
            return index.Fields;
        }

        #region Create and configure index

        protected virtual Task CreateIndex(string indexName, IList<Field> providerFields)
        {
            var index = CreateIndexDefinition(indexName, providerFields);
            return Client.Indexes.CreateAsync(index);
        }

        protected virtual Index CreateIndexDefinition(string indexName, IList<Field> providerFields)
        {
            var minGram = GetMinGram();
            var maxGram = GetMaxGram();

            var index = new Index
            {
                Name = indexName,
                Fields = providerFields.OrderBy(f => f.Name).ToArray(),
                TokenFilters = new TokenFilter[]
                {
                    new NGramTokenFilterV2(ModuleConstants.NGramFilterName, minGram, maxGram),
                    new EdgeNGramTokenFilterV2(ModuleConstants.EdgeNGramFilterName, minGram, maxGram)
                },
                Analyzers = new Analyzer[]
                {
                    new CustomAnalyzer
                    {
                        Name = ContentAnalyzerName,
                        Tokenizer = TokenizerName.Standard,
                        TokenFilters = new[] { TokenFilterName.Lowercase, GetTokenFilterName() }
                    }
                },
            };

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

            if (suggestSourceFields.Any())
            {
                var suggester = new Suggester(SuggesterName, suggestSourceFields);
                index.Suggesters = new[] { suggester };
            }

            return index;
        }

        protected virtual string GetTokenFilterName()
        {
            return _settingsManager.GetValue<string>(ModuleConstants.Settings.Indexing.TokenFilter);
        }

        protected virtual int GetMinGram()
        {
            return _settingsManager.GetValue<int>(ModuleConstants.Settings.Indexing.MinGram);
        }

        protected virtual int GetMaxGram()
        {
            return _settingsManager.GetValue<int>(ModuleConstants.Settings.Indexing.MaxGram);
        }

        #endregion

        #region Mapping

        protected virtual async Task<IList<Field>> GetMappingAsync(string indexName)
        {
            var providerFields = GetMappingFromCache(indexName);
            if (providerFields == null && await IndexExistsAsync(indexName))
            {
                providerFields = await GetIndexFields(indexName);
            }

            providerFields ??= new List<Field>();
            AddMappingToCache(indexName, providerFields);

            return providerFields;
        }

        protected virtual void UpdateMapping(string indexName, IList<Field> providerFields)
        {
            var index = CreateIndexDefinition(indexName, providerFields);

            try
            {
                var updatedIndex = Client.Indexes.CreateOrUpdate(indexName, index);
                AddMappingToCache(indexName, updatedIndex.Fields);
            }
            catch (CloudException cloudException)
            {
                ThrowException(cloudException, providerFields: providerFields);
            }
        }

        protected virtual IList<Field> GetMappingFromCache(string indexName)
        {
            return _mappings.TryGetValue(indexName, out var mapping) ? mapping : null;
        }

        protected virtual void AddMappingToCache(string indexName, IList<Field> providerFields)
        {
            _mappings[indexName] = providerFields;
        }

        protected virtual void RemoveMappingFromCache(string indexName)
        {
            _mappings.TryRemove(indexName, out _);
        }

        #endregion

        protected virtual void ThrowException(string message, Exception innerException)
        {
            throw new SearchException($"{message}. Search service name: {_azureSearchOptions.SearchServiceName}, Scope: {_searchOptions.Scope}", innerException);
        }

        private void ThrowException(CloudException cloudException, IList<Field> providerFields = null, IEnumerable<SearchDocument> providerDocuments = null)
        {
            var error = WrapCloudExceptionMessage(cloudException);

            if (providerDocuments != null)
            {
                var documentIds = string.Join(',', providerDocuments.Select(x => x.Id));
                error = $"{error}; DocumentIds:{documentIds}";
            }

            if (providerFields != null)
            {
                var fieldNames = string.Join(',', providerFields.Select(x => x.Name));
                error = $"{error}; FieldNames:{fieldNames}";
            }

            throw new SearchException(error, cloudException);
        }

        protected virtual SearchServiceClient CreateSearchServiceClient()
        {
            var result = new SearchServiceClient(_azureSearchOptions.SearchServiceName, new SearchCredentials(_azureSearchOptions.Key));
            return result;
        }

        protected virtual ISearchIndexClient GetSearchIndexClient(string indexName)
        {
            return Client.Indexes.GetClient(indexName);
        }

        /// <summary>
        /// Construct exception message since CloudException.Message doesn't contain details (sometimes)
        /// </summary>
        protected virtual string WrapCloudExceptionMessage(CloudException exception)
        {
            if (string.IsNullOrEmpty(exception.Response?.Content))
            {
                return exception.ToString();
            }

            var unescapedContent = Regex.Unescape(exception.Response.Content);
            return $"StatusCode: {exception.Response.StatusCode}; Content:{unescapedContent}";
        }
    }
}
