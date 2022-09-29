using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VirtoCommerce.AzureSearchModule.Data;
using Xunit;

namespace VirtoCommerce.AzureSearchModule.Tests
{
    public class FuzzyConvertorTest
    {
        [Fact]
        public void GetSimpleFuzzySearchText()
        {
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("university", null);
            Assert.Equal("university~", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextWithLevel()
        {
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("university", 2);
            Assert.Equal("university~2", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextMultiTerms()
        {
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("university of washington", null);
            Assert.Equal("university~ of~ washington~", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextMultiTermsWithLevel()
        {
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("university of washington", 3);
            Assert.Equal("university~3 of~3 washington~3", result);
        }
    }
}
