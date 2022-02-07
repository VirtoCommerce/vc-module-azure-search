# Virto Commerce Azure Search Module

[![CI status](https://github.com/VirtoCommerce/vc-module-azure-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-azure-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search)

VirtoCommerce.AzureSearch module implements ISearchProvider defined in the VirtoCommerce Search module and uses <a href="https://azure.microsoft.com/en-us/services/search/" target="_blank">Azure Search</a>.

## Configuration
Azure Search provider are configurable by these configuration keys:

* **Search.Provider** is the name of the search provider and must be **AzureSearch**
* **Search.AzureSearch.SearchServiceName** is the name of the search service instance in your Azure account Ex: SERVICENAME.search.windows.net.
* **Search.AzureSearch.AccessKey** is the primary or secondary admin key for this search service.
* **Search.Scope** is a common name (prefix) of all indexes. Each document type is stored in a separate index. Full index name is `scope-{documenttype}`. One search service can serve multiple indexes.

[Read more about configuration here](https://virtocommerce.com/docs/user-guide/configuration-settings/)

# Known limitations
* Collections in a document can only contain strings. This means you cannot index a document with a collection of numbers.
* Document field names can only contain letters, digits and underscores. When indexing a document with invalid characters in a field name, all such characters will be replaced with an underscore, which can lead to duplicate fields.

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
