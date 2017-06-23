# VirtoCommerce.AzureSearch
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
Azure Search service supports only one set of filters which apply both to the facets and to the search request itself. This leads to the following issues:
* If you filter documents by a field value (e.g. color:red), a facet by this field returns only this value counter.
* Facets cannot be calculated by a filter only as it could be done with Elasticsearch, so the aggregation by a price range using multiple pricelists ordered by priority is not supported.

Both issues can be resolved by sending additional requests to the Azure Search service, and this behavior will be implemented in the next version of this module.

# License
Copyright (c) Virtosoftware Ltd. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
