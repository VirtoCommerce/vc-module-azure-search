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
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("blue",null);
            Assert.Equal("blue~", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextWithLevel()
        {
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("blue", 2);
            Assert.Equal("blue~2", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextMultiTerms()
        {
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("blue dress",null);
            Assert.Equal("blue~ dress~", result);
        }

        [Fact]
        public void GetSimpleFuzzySearchTextMultiTermsWithLevel()
        {
            var result = AzureSearchRequestBuilder.GetFuzzySearchText("blue dress",3);
            Assert.Equal("blue~3 dress~3", result);
        }
    }
}
