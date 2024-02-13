using VirtoCommerce.AzureSearchModule.Data;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Tests
{
    public class FuzzyConvertorTest
    {
        private readonly AzureSearchDocumentsRequestBuilder _queryBuilder = new();

        [Fact]
        public void GetSimpleFuzzySearchText()
        {
            var result = _queryBuilder.GetFuzzySearchText("university", null);
            Assert.Equal("university~", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextWithLevel()
        {
            var result = _queryBuilder.GetFuzzySearchText("university", 2);
            Assert.Equal("university~2", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextMultiTerms()
        {
            var result = _queryBuilder.GetFuzzySearchText("university of washington", null);
            Assert.Equal("university~ of~ washington~", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextMultiTermsWithLevel()
        {
            var result = _queryBuilder.GetFuzzySearchText("university of washington", 3);
            Assert.Equal("university~3 of~3 washington~3", result);
        }
    }
}
