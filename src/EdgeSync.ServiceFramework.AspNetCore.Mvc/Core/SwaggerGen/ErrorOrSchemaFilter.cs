using Microsoft.OpenApi.Models;

using Swashbuckle.AspNetCore.SwaggerGen;

namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Core.SwaggerGen;

/// <summary>
/// Schema filter to unwrap ErrorOr types in Swagger documentation
/// </summary>
public class ErrorOrSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        var type = context.Type;
        
        // Check if this is an ErrorOr<T> type
        if (IsErrorOrType(type))
        {
            var unwrappedType = GetErrorOrInnerType(type);
            if (unwrappedType != null)
            {
                // Generate schema for the inner type instead
                var unwrappedSchema = context.SchemaGenerator.GenerateSchema(unwrappedType, context.SchemaRepository);
                
                // Copy all properties from the unwrapped schema
                schema.Type = unwrappedSchema.Type;
                schema.Format = unwrappedSchema.Format;
                schema.Properties = unwrappedSchema.Properties;
                schema.Items = unwrappedSchema.Items;
                schema.Reference = unwrappedSchema.Reference;
                schema.AllOf = unwrappedSchema.AllOf;
                schema.OneOf = unwrappedSchema.OneOf;
                schema.AnyOf = unwrappedSchema.AnyOf;
                schema.AdditionalProperties = unwrappedSchema.AdditionalProperties;
                schema.Required = unwrappedSchema.Required;
            }
        }
    }

    private bool IsErrorOrType(Type type)
    {
        return type.IsGenericType && 
               type.GetGenericTypeDefinition().FullName == "ErrorOr.ErrorOr`1";
    }

    private Type? GetErrorOrInnerType(Type errorOrType)
    {
        if (IsErrorOrType(errorOrType))
        {
            return errorOrType.GetGenericArguments()[0];
        }
        return null;
    }
}