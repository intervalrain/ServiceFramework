namespace EdgeSync.ServiceFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class ServiceHandlerAttribute : Attribute
{
    public string EndpointName { get; }
    public string? CustomSubject { get; }

    public ServiceHandlerAttribute(string endpointName, string? customSubject = null)
    {
        EndpointName = endpointName;
        CustomSubject = customSubject;
    }
}