using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.Search.Models;
using Newtonsoft.Json;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.AzureSearchModule.Data
{
    public class AzureSearchFieldBuilder
    {
        public string ContentAnalyzerName { get; private set; }
        private readonly IDictionary<string, DataType> _dataTypeMemo = new Dictionary<string, DataType>();
        private readonly IDictionary<string, bool> _typeComplexityMemo = new Dictionary<string, bool>();

        public AzureSearchFieldBuilder(string contentAnalyzerName)
        {
            ContentAnalyzerName = contentAnalyzerName;
        }

        public IList<Field> BuildFields(Type type, int maxDepth = 5, bool disableSortingForSubFields = false) => type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(prop => prop.CanRead)
            .Where(prop => prop.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            .Select(prop => BuildField(prop, maxDepth, disableSortingForSubFields))
            .Where(field => field != null)
            .ToList();

        public Field BuildField(PropertyInfo propertyInfo, int maxDepth, bool disableSortingForSubFields = false)
        {
            var propertyType = propertyInfo.PropertyType;
            var fieldName = propertyInfo.Name;

            var memoTypeComplexityKey = propertyType.FullName;
            var isComplex = _typeComplexityMemo.ContainsKey(memoTypeComplexityKey)
                ? _typeComplexityMemo[memoTypeComplexityKey]
                : IsComplex(propertyType, _typeComplexityMemo);

            var isCollection = IsGenericCollection(propertyType);
            // todo: split by methods
            if (!isComplex && !isCollection)
            {
                var memoDataTypeKey = propertyType.FullName;

                var providerFieldType = memoDataTypeKey != null && _dataTypeMemo.ContainsKey(memoDataTypeKey)
                    ? _dataTypeMemo[memoDataTypeKey]
                    : Map(propertyType);

                if (providerFieldType == default)
                {
                    return null;
                }

                _dataTypeMemo.TryAdd(memoDataTypeKey, providerFieldType);

                var field = new Field(fieldName, providerFieldType)
                {
                    IsKey = fieldName == AzureSearchHelper.KeyFieldName,
                    IsRetrievable = true,
                    IsSearchable = providerFieldType == DataType.String,
                    IsFilterable = true,
                    IsFacetable = providerFieldType != DataType.GeographyPoint,
                    IsSortable = !disableSortingForSubFields
                };

                if (field.IsSearchable == true)
                {
                    field.IndexAnalyzer = ContentAnalyzerName;
                    field.SearchAnalyzer = AnalyzerName.StandardLucene;
                }

                return field;
            }

            // Overflow protection
            if (maxDepth <= 0)
                return null;

            IList<Field> subFields;

            // todo: split by methods
            if (IsGenericCollection(propertyType))
            {
                var subType = propertyType.GetGenericArguments()[0];

                subFields = BuildFields(subType, --maxDepth, disableSortingForSubFields: true);
                if (!subFields.Any())
                    return null;

                return new Field(fieldName, DataType.Collection(DataType.Complex), subFields);
            }

            // todo: split by methods
            subFields = BuildFields(propertyType, --maxDepth, disableSortingForSubFields);
            if (!subFields.Any())
                return null;

            return new Field(fieldName, DataType.Complex, subFields);
        }

        public static bool IsComplex(Type type, IDictionary<string, bool> memo)
        {
            var result = type.IsSubclassOf(typeof(Entity)) || type.IsSubclassOf(typeof(ValueObject));

            memo.TryAdd(type.FullName, result);

            return result;
        }

        public static bool IsGenericCollection(Type type)
        {
            if (type.IsArray)
                return false;

            return type.GetInterfaces().Any(x => x.IsGenericType
                    && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    && x.GetGenericArguments().Any(y => !y.IsSubclassOf(typeof(ValueType))));
        }

        public static DataType Map(Type type)
        {
            if (type == null)
            {
                return default;
            }

            // Todo: rewrite to switch patternt matching from C# 9.0
            if (type == typeof(string) || type == typeof(object))
                return DataType.String;

            if (type == typeof(int) || type == typeof(int?) || type.IsEnum)
                return DataType.Int32;

            if (type == typeof(long) || type == typeof(long?))
                return DataType.Int64;

            if (type == typeof(double) || type == typeof(double?) || type == typeof(decimal) || type == typeof(decimal?))
                return DataType.Double;

            if (type == typeof(bool) || type == typeof(bool?))
                return DataType.Boolean;

            if (type == typeof(DateTimeOffset) || type == typeof(DateTime) || type == typeof(DateTime?))
                return DataType.DateTimeOffset;

            if (type == typeof(GeoPoint))
                return DataType.GeographyPoint;

            // string[], bool[], etc
            if (type.IsArray || type.IsSubclassOf(typeof(IEnumerable)))
            {
                var innerDataType = Map(type.GetElementType());
                if (innerDataType != default)
                {
                    return DataType.Collection(innerDataType);
                }
            }

            return default;
            //throw new ArgumentException($"Unsupported type {fieldType}", nameof(fieldType)); Здесь могла быть ваша реклама
        }
    }
}
