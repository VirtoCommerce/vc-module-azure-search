# VirtoCommerce.AzureSearch

[![CI status](https://github.com/VirtoCommerce/vc-module-azure-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-azure-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-azure-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-azure-search)

VirtoCommerce.AzureSearch module implements ISearchProvider defined in the VirtoCommerce.Core module and uses cloud search service <a href="https://azure.microsoft.com/en-us/services/search/" target="_blank">Azure Search</a>.

# Installation
Installing the module:
* Automatically: in VC Manager go to **Modules > Available**, select the **Azure Search module** and click **Install**.
* Manually: download module ZIP package from https://github.com/VirtoCommerce/vc-module-azure-search/releases. In VC Manager go to **Modules > Advanced**, upload module package and click **Install**.

# Configuration
## VirtoCommerce.Search.SearchConnectionString
The search configuration string is a text string consisting of name/value pairs seaprated by semicolon (;). Name and value are separated by equal sign (=).
For Azure Search provider the configuration string must have four parameters:
```
provider=AzureSearch;server=servicename;key=accesskey;scope=default
```
* **provider** is the name of the search provider and must be **AzureSearch**
* **ServiceName** or **server** is the name of the search service instance in your Azure account (https://SERVICENAME.search.windows.net).
* **AccessKey** or **key** is the primary or secondary admin key for this search service.
* **scope** is a common name (prefix) of all indexes. Each document type is stored in a separate index. Full index name is `scope-documenttype`. One search service can serve multiple indexes.

You can configure the search configuration string either in the VC Manager UI or in VC Manager web.config. Web.config has higher priority.
* VC Manager: **Settings > Search > General > Search configuration string**
* web.config: **connectionStrings > SearchConnectionString**:
```
<connectionStrings>
    <add name="SearchConnectionString" connectionString="provider=AzureSearch;server=servicename;key=accesskey;scope=default" />
</connectionStrings>
```

# Known issues
* Collections in a document can only contain strings. This means you cannot index a document with a collection of numbers.
* Document field names can only contain letters, digits and underscores. When indexing a document with invalid characters in a field name, all such characters will be replaced with an underscore, which can lead to duplicate fields.

# License
Copyright (c) Virto Solutions LTD. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
