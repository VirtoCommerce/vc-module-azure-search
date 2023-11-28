using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.AzureSearchModule.Data
{
    [ExcludeFromCodeCoverage]
    public class ModuleConstants
    {
        public const string ProviderName = "AzureSearch";

        public const string NGramFilterName = "custom_ngram";
        public const string EdgeNGramFilterName = "custom_edge_ngram";

        public static class Settings
        {
            public static class Indexing
            {
                public static SettingDescriptor TokenFilter { get; } = new()
                {
                    Name = "VirtoCommerce.Search.AzureSearch.TokenFilter",
                    GroupName = "Search|Azure Search",
                    ValueType = SettingValueType.ShortText,
                    DefaultValue = EdgeNGramFilterName,
                    AllowedValues = new object[] { EdgeNGramFilterName, NGramFilterName },
                };

                public static SettingDescriptor MinGram { get; } = new()
                {
                    Name = "VirtoCommerce.Search.AzureSearch.NGramTokenFilter.MinGram",
                    GroupName = "Search|Azure Search",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 1,
                };

                public static SettingDescriptor MaxGram { get; } = new()
                {
                    Name = "VirtoCommerce.Search.AzureSearch.NGramTokenFilter.MaxGram",
                    GroupName = "Search|Azure Search",
                    ValueType = SettingValueType.Integer,
                    DefaultValue = 20,
                };

                public static IEnumerable<SettingDescriptor> AllIndexingSettings
                {
                    get
                    {
                        yield return TokenFilter;
                        yield return MinGram;
                        yield return MaxGram;
                    }
                }
            }

            public static IEnumerable<SettingDescriptor> AllSettings => Indexing.AllIndexingSettings;
        }
    }
}
