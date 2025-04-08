namespace EdgeSync.ServiceFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SubjectAttribute : Attribute
{
    public string EndpointName { get; }
    public string? CustomSubject { get; }

    public SubjectAttribute(string endpointName, string? customSubject = null)
    {
        EndpointName = endpointName;
        CustomSubject = customSubject;
    }
}