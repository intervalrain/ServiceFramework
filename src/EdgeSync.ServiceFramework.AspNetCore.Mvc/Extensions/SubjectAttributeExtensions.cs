using EdgeSync.ServiceFramework.AspNetCore.Mvc.Extensions;
using EdgeSync.ServiceFramework.Attributes;

public static class SubjectAttributeExtensions
{
    /// <summary>
    /// Gets the API endpoint, using automatic naming if not explicitly set
    /// </summary>
    /// <param name="methodName">The method name to generate endpoint from</param>
    /// <returns>The API endpoint</returns>
    public static string GetEndpoint(this SubjectAttribute attribute, string methodName)
    {
        if (!string.IsNullOrEmpty(attribute.EndpointName))
            return attribute.EndpointName;

        return GenerateEndpointFromMethodName(methodName);
    }

    private static string GenerateEndpointFromMethodName(string methodName)
    {
        var baseName = methodName.RemovePostfixes("Async");

        string action;
        string entity;

        if (baseName.StartsWith("Get") && baseName != "Get")
        {
            action = "get";
            entity = baseName.RemovePrefixes("Get");
        }
        else if (baseName.StartsWith("Create"))
        {
            action = "create";
            entity = baseName.RemovePrefixes("Create");
        }
        else if (baseName.StartsWith("Update"))
        {
            action = "update";
            entity = baseName.RemovePrefixes("Update");
        }
        else if (baseName.StartsWith("Delete"))
        {
            action = "delete";
            entity = baseName.RemovePrefixes("Delete");
        }
        else if (baseName.StartsWith("List") || baseName.EndsWith("List"))
        {
            action = "list";
            entity = baseName.RemovePrefixes("List").RemovePostfixes("List");
        }
        else
        {
            action = baseName.ToLower();
            entity = "";
        }

        if (!string.IsNullOrEmpty(entity))
        {
            entity = ConvertPascalCaseToLowercase(entity);
            return $"{entity}.{action}";
        }

        return action;
    }

    private static string ConvertPascalCaseToLowercase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input.ToLower();
    }
}