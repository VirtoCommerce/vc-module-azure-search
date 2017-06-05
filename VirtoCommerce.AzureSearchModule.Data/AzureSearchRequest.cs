using System;
using Microsoft.Azure.Search.Models;

namespace VirtoCommerce.AzureSearchModule.Data
{
    [CLSCompliant(false)]
    public class AzureSearchRequest
    {
        public string SearchText { get; set; }
        public SearchParameters SearchParameters { get; set; }
    }
}
