namespace EdgeSync.ServiceFramework.Exceptions;

/// <summary>
/// Exception thrown when a duplicate connection name is detected
/// </summary>
public class DuplicateNameException : Exception
{
    public string ConnectionName { get; }

    public DuplicateNameException(string connectionName) 
        : base($"Connection name '{connectionName}' already exists in the configuration")
    {
        ConnectionName = connectionName;
    }

    public DuplicateNameException(string connectionName, string message) 
        : base(message)
    {
        ConnectionName = connectionName;
    }

    public DuplicateNameException(string connectionName, string message, Exception innerException) 
        : base(message, innerException)
    {
        ConnectionName = connectionName;
    }
}