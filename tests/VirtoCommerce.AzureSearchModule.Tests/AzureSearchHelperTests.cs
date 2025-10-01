using VirtoCommerce.AzureSearchModule.Data;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Tests;
public class AzureSearchHelperTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("One--Two..Three  Four", "f_one__two__three__four")]
    public void ToAzureFieldNameTests(string name, string expectedName)
    {
        var result = AzureSearchHelper.ToAzureFieldName(name);
        Assert.Equal(expectedName, result);
    }
}
