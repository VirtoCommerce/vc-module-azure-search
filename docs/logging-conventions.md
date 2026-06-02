# Logging conventions

These conventions apply to all logging in this module. They follow the standard
structured-logging pattern supported natively by `Microsoft.Extensions.Logging`
and Serilog (message templates with named placeholders), so that downstream sinks
(e.g. New Relic) can group, filter, alert, and build dashboards on log events.

## Rules

1. **The message is a stable template, identical for every occurrence of the same
   logical event.** Never build the message with string concatenation or `$"..."`
   interpolation. Put variable data in named placeholders instead.

   ```csharp
   // ❌ Don't — unique string per call, not groupable
   _logger.LogError(ex, $"Failed to index documents in {indexName} on {serviceName}");

   // ✅ Do — stable template + structured properties
   _logger.LogError(ex,
       "Failed to index documents on index {SearchIndex} in service {SearchService}",
       indexName, serviceName);
   ```

2. **Always pass the exception as the `Exception` argument** (first argument of
   `LogError`), never concatenated into the message. The logging pipeline serializes
   it into structured exception fields (including inner exceptions and stack trace).

3. **Placeholder names are PascalCase** and drawn from the shared glossary below.
   Placeholders bind to arguments **positionally**, so the placeholder order in the
   template must match the argument order.

4. **Keep cardinality low.** Do not put per-request high-cardinality values
   (document ids, field-name lists, raw response bodies) into the structured error
   event — they defeat grouping and inflate ingest cost. The underlying
   `RequestFailedException` already carries the Azure response detail. If such values
   are needed for debugging, emit them in a separate `LogDebug` line.

5. **Templates must be constant expressions.** Analyzer `CA2254` (enforced as an
   error via `TreatWarningsAsErrors`) forbids a variable/parameter template. The
   literal template must live at the call site (or be a `const`); only value
   extraction may be factored into a helper.

## Glossary

| Property         | Meaning                                                        |
|------------------|----------------------------------------------------------------|
| `SearchService`  | Azure Search service name (`AzureSearchOptions.SearchServiceName`) |
| `SearchIndex`    | Target index or index alias name                               |
| `DocumentType`   | Search document type (e.g. `member`, `product`)                |
| `Operation`      | Logical operation / method name (`nameof(...)`)                |
| `HttpStatus`     | HTTP status code from `RequestFailedException.Status`          |
| `ErrorCode`      | Service error code from `RequestFailedException.ErrorCode`     |
| `AzureRequestId` | Azure request id (`x-ms-request-id` response header)           |

Validation events (e.g. field type mismatch) additionally use: `DocumentId`,
`FieldName`, `DocumentFieldType`, `SchemaFieldType`.

## Pattern: log-and-throw

Catch sites that wrap an Azure error log the structured event (authoritative,
Azure-specific) and then throw a `SearchException` with a **stable** message and the
original exception as the inner exception — no concatenated blob:

```csharp
catch (RequestFailedException ex)
{
    _logger.LogError(ex,
        "Failed to remove documents in {Operation} for {DocumentType} on index {SearchIndex} in service {SearchService}. HTTP {HttpStatus}, error code {ErrorCode}, request id {AzureRequestId}",
        nameof(RemoveAsync), documentType, indexName, _azureSearchOptions.SearchServiceName,
        ex.Status, ex.ErrorCode, GetAzureRequestId(ex));

    throw new SearchException("Failed to remove documents", ex);
}
```

### Before / after

```text
// Before — single multi-kilobyte, non-groupable message
StatusCode: 404; Content:The index 'sadevdefault-active-member' ... DocumentIds:... ; FieldNames:...

// After — stable template + discrete fields
message:        "Failed to index documents in IndexWithRetryAsync for member on index sadevdefault-active-member in service hei-selo-dev-ne-srch-1. HTTP 404, error code ..., request id 35e05517-..."
Operation:      "IndexWithRetryAsync"
DocumentType:   "member"
SearchIndex:    "sadevdefault-active-member"
SearchService:  "hei-selo-dev-ne-srch-1"
HttpStatus:     404
AzureRequestId: "35e05517-4cc1-4f20-bbd1-392266d0cbfc"
exception:      <structured exception object, incl. inner exceptions and stack>
```
