using Azure.Search.Documents.Indexes.Models;
using Microsoft.Azure.Search.Models;

namespace VirtoCommerce.AzureSearchModule.Data;

public static class FieldExtensions
{
    public static bool IsCollection(this Field field)
    {
        var fieldType = field.Type.ToString();
        return fieldType != null && fieldType.StartsWith("Collection(");
    }

    public static bool IsCollection(this SearchField field)
    {
        var fieldType = field.Type.ToString();
        return fieldType != null && fieldType.StartsWith("Collection(");
    }
}
