namespace EdgeSync.ServiceFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class SubjectAttribute : Attribute
{
    public string EndpointName { get; set; }
    public string CustomSubject { get; set; }

    public SubjectAttribute(string endpointName, string customSubject)
    {
        EndpointName = endpointName;
        CustomSubject = customSubject;
    }
}