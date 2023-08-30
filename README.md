# Virto Commerce Azure Search Module

[![CI status](https://github.com/VirtoCommerce/vc-module-azure-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-azure-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search)

VirtoCommerce.AzureSearch module implements ISearchProvider defined in the VirtoCommerce Search module and uses <a href="https://azure.microsoft.com/en-us/services/search/" target="_blank">Azure Cognitive Search</a> engine.

## Configuration
Azure Search provider are configurable by these configuration keys:

* **Search.Provider** is the name of the search provider and must be **AzureSearch**
* **Search.AzureSearch.SearchServiceName** is the name of the search service instance in your Azure account Ex: SERVICENAME.search.windows.net.
* **Search.AzureSearch.AccessKey** is the primary or secondary admin key for this search service.
* **Search.AzureSearch.QueryParserType** is type of Query Languages. Simple (default) or Full.
* **Search.Scope** is a common name (prefix) of all indexes. Each document type is stored in a separate index. Full index name is `scope-{documenttype}`. One search service can serve multiple indexes.


[Read more about configuration here](https://virtocommerce.com/docs/user-guide/configuration-settings/)

## Query Languages

Azure Cognitive Search implements two query languages:
1. [Simple Query](https://learn.microsoft.com/en-us/azure/search/query-simple-syntax)
2. [Lucene/Full Query](https://learn.microsoft.com/en-us/azure/search/search-query-lucene-examples)

The simple parser is more flexible and will attempt to interpret a request even if it's not perfectly composed.
Because it's flexible, it's the default for queries in Azure Cognitive Search.

The Lucene/Full parser supports complex query formats, such as field-scoped queries, fuzzy search, infix and suffix wildcard search, proximity search, term boosting, and regular expression search.
The additional power comes with additional processing requirements so you should expect a slightly longer execution time. 


## Fuzzy Search

Azure Cognitive Search supports fuzzy search a type of query that compensates for typos and misspelled terms in the input string.

Two options to activate fuzzy search:
1. **Manually.** Set `QueryParserType` to `Full` in configuration and append a tilde character(~) after each whole term manually. [Read about fuzzy query here](https://learn.microsoft.com/en-us/azure/search/search-query-fuzzy)
2. **Automaticaly.** Set `SearchRequest.IsFuzzySearch` to true. Virto Commerce Azure Search Module activates Full query parser and adjust the query term with followed by a tilde (\~) operator at the end of each whole term.
For example, if your query (`SearchRequest.SearchKeywords`) has three terms "university of washington", the module adjusts every term in the query to "university\~ of\~ washington\~".

> The default distance (`SearchRequest.Fuzziness`) is 2. 

## Known limitations

* Collections in a document can only contain strings. This means you cannot index a document with a collection of numbers.
* Document field names can only contain letters, digits and underscores. When indexing a document with invalid characters in a field name, all such characters will be replaced with an underscore, which can lead to duplicate fields.
* When `QueryParserType` is set to `Full`, [Lucene syntax rules](https://learn.microsoft.com/en-us/azure/search/query-lucene-syntax) are applied to the search string. 
  In order to use any of the search operators as part of the search text, [escape](https://learn.microsoft.com/en-us/azure/search/query-lucene-syntax#escaping-special-characters) the character by prefixing it with a single backslash (`\`).

## Documentation

* [Search Fundamentals](https://virtocommerce.com/docs/fundamentals/search/)

## References

* Deploy: https://virtocommerce.com/docs/latest/developer-guide/deploy-module-from-source-code/
* Installation: https://www.virtocommerce.com/docs/latest/user-guide/modules/
* Home: https://virtocommerce.com
* Community: https://www.virtocommerce.org
* [Download Latest Release](https://github.com/VirtoCommerce/vc-module-catalog/releases/latest)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
