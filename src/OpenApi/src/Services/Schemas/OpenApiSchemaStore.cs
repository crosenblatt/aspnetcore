// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO.Pipelines;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace Microsoft.AspNetCore.OpenApi;

/// <summary>
/// Stores schemas generated by the JsonSchemaMapper for a
/// given OpenAPI document for later resolution.
/// </summary>
internal sealed class OpenApiSchemaStore
{
    private readonly Dictionary<OpenApiSchemaKey, JsonNode> _schemas = new()
    {
        // Pre-populate OpenAPI schemas for well-defined types in ASP.NET Core.
        [new OpenApiSchemaKey(typeof(IFormFile), null)] = new JsonObject
        {
            ["type"] = "string",
            ["format"] = "binary",
            [OpenApiConstants.SchemaId] = "IFormFile"
        },
        [new OpenApiSchemaKey(typeof(IFormFileCollection), null)] = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["type"] = "string",
                ["format"] = "binary",
                [OpenApiConstants.SchemaId] = "IFormFile"
            },
            [OpenApiConstants.SchemaId] = "IFormFileCollection"
        },
        [new OpenApiSchemaKey(typeof(Stream), null)] = new JsonObject
        {
            ["type"] = "string",
            ["format"] = "binary",
            [OpenApiConstants.SchemaId] = "Stream"
        },
        [new OpenApiSchemaKey(typeof(PipeReader), null)] = new JsonObject
        {
            ["type"] = "string",
            ["format"] = "binary",
            [OpenApiConstants.SchemaId] = "PipeReader"
        },
    };

    public readonly Dictionary<OpenApiSchema, string?> SchemasByReference = new(OpenApiSchemaComparer.Instance);
    private readonly Dictionary<string, int> _referenceIdCounter = new();

    /// <summary>
    /// Resolves the JSON schema for the given type and parameter description.
    /// </summary>
    /// <param name="key">The key associated with the generated schema.</param>
    /// <param name="valueFactory">A function used to generated the JSON object representing the schema.</param>
    /// <returns>A <see cref="JsonObject" /> representing the JSON schema associated with the key.</returns>
    public JsonNode GetOrAdd(OpenApiSchemaKey key, Func<OpenApiSchemaKey, JsonNode> valueFactory)
    {
        if (_schemas.TryGetValue(key, out var schema))
        {
            return schema;
        }
        var targetSchema = valueFactory(key);
        _schemas.Add(key, targetSchema);
        return targetSchema;
    }

    /// <summary>
    /// Add the provided schema to the schema-with-references cache that is eventually
    /// used to populate the top-level components.schemas object. This method will
    /// unwrap the provided schema and add any child schemas to the global cache. Child
    /// schemas include those referenced in the schema.Items, schema.AdditionalProperties, or
    /// schema.Properties collections. Schema reference IDs are only set for schemas that have
    /// been encountered more than once in the document to avoid unnecessarily capturing unique
    /// schemas into the top-level document.
    /// </summary>
    /// <param name="schema">The <see cref="OpenApiSchema"/> to add to the schemas-with-references cache.</param>
    public void PopulateSchemaIntoReferenceCache(OpenApiSchema schema)
    {
        AddOrUpdateSchemaByReference(schema);
        if (schema.AdditionalProperties is not null)
        {
            AddOrUpdateSchemaByReference(schema.AdditionalProperties);
        }
        if (schema.Items is not null)
        {
            AddOrUpdateSchemaByReference(schema.Items);
        }
        if (schema.AllOf is not null)
        {
            foreach (var allOfSchema in schema.AllOf)
            {
                AddOrUpdateSchemaByReference(allOfSchema);
            }
        }
        if (schema.AnyOf is not null)
        {
            var parentSchemaIdea = schema.Extensions[OpenApiConstants.SchemaId] is OpenApiString { Value: string schemaId }
                ? schemaId
                : null;
            foreach (var anyOfSchema in schema.AnyOf)
            {
                AddOrUpdateSchemaByReference(anyOfSchema, parentSchemaIdea);
            }
        }
        if (schema.Properties is not null)
        {
            foreach (var property in schema.Properties.Values)
            {
                AddOrUpdateSchemaByReference(property);
            }
        }
    }

    private void AddOrUpdateSchemaByReference(OpenApiSchema schema, string? parentSchemaId = null)
    {
        var targetReferenceId = parentSchemaId is not null ? $"{parentSchemaId}{GetSchemaReferenceId(schema)}" : GetSchemaReferenceId(schema);
        if (SchemasByReference.TryGetValue(schema, out var referenceId))
        {
            // If we've already used this reference ID else where in the document, increment a counter value to the reference
            // ID to avoid name collisions. These collisions are most likely to occur when the same .NET type produces a different
            // schema in the OpenAPI document because of special annotations provided on it. For example, in the two type definitions
            // below:
            // public class Todo
            // {
            //     public int Id { get; set; }
            //     public string Name { get; set; }
            // }
            // public class Project
            // {
            //     public int Id { get; set; }
            //     [MinLength(5)]
            //     public string Title { get; set; }
            // }
            // The `Title` and `Name` properties are both strings but the `Title` property has a `minLength` annotation
            // on it that will materialize into a different schema.
            // {
            //
            //      "type": "string",
            //      "minLength": 5
            // }
            // {
            //      "type": "string"
            // }
            // In this case, although the reference ID  based on the .NET type we would use is `string`, the
            // two schemas are distinct.
            if (referenceId == null)
            {
                if (_referenceIdCounter.TryGetValue(targetReferenceId, out var counter))
                {
                    counter++;
                    _referenceIdCounter[targetReferenceId] = counter;
                    SchemasByReference[schema] = $"{targetReferenceId}{counter}";
                }
                else
                {
                    _referenceIdCounter[targetReferenceId] = 1;
                    SchemasByReference[schema] = targetReferenceId;
                }
            }
        }
        else
        {
            SchemasByReference[schema] = parentSchemaId is not null ? targetReferenceId : null;
        }
    }

    private static string GetSchemaReferenceId(OpenApiSchema schema)
    {
        if (schema.Extensions.TryGetValue(OpenApiConstants.SchemaId, out var referenceIdAny)
            && referenceIdAny is OpenApiString { Value: string referenceId })
        {
            return referenceId;
        }

        throw new InvalidOperationException("The schema reference ID must be set on the schema.");
    }
}
