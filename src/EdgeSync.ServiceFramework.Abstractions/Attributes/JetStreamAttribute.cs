namespace EdgeSync.ServiceFramework.Abstractions.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class JetStreamAttribute : Attribute
{
    public bool Enable { get; }

    public JetStreamAttribute(bool Enable = true)
    {
        this.Enable = Enable;
    }
}