using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.AzureSearchModule.Data.Extensions
{
    public static class SettingsExtensions
    {
        public static string GetTokenFilterName(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<string>(ModuleConstants.Settings.Indexing.TokenFilter);
        }

        public static int GetMinGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<int>(ModuleConstants.Settings.Indexing.MinGram);
        }

        public static int GetMaxGram(this ISettingsManager settingsManager)
        {
            return settingsManager.GetValue<int>(ModuleConstants.Settings.Indexing.MaxGram);
        }
    }
}
