using ProtobufEmpty = Google.Protobuf.WellKnownTypes.Empty;

namespace EdgeSync.ServiceFramework.Data;

/// <summary>
/// Helper class for Google.Protobuf.WellKnownTypes.Empty for handling parameterless methods 
/// and object type serialization compatibility with Protobuf.
/// Uses alias to avoid naming conflicts with System.Type.
/// </summary>
public static class EmptyMessage
{
    /// <summary>
    /// Gets the default instance of Google.Protobuf.WellKnownTypes.Empty
    /// </summary>
    public static ProtobufEmpty DefaultInstance => new();

    /// <summary>
    /// Creates a new instance of Google.Protobuf.WellKnownTypes.Empty
    /// </summary>
    public static ProtobufEmpty Create() => new ProtobufEmpty();
}