using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Core.Serializers;


/// <summary>
/// Interface for the serializer adapter factory
/// </summary>
public interface ISerializerAdapterFactory
{
    /// <summary>
    /// Gets the appropriate adapter for the specified serializer registry
    /// </summary>
    /// <param name="serializerRegistry">The serializer registry</param>
    /// <returns>The adapter that can handle the serializer registry</returns>
    ISerializerAdapter GetAdapter(INatsSerializerRegistry serializerRegistry);

    /// <summary>
    /// Gets the appropriate typed adapter for the specified serializer registry
    /// </summary>
    /// <param name="serializerRegistry">The serializer registry</param>
    /// <returns>The typed adapter that can handle the serializer registry</returns>
    ITypedSerializerAdapter GetTypedAdapter(INatsSerializerRegistry serializerRegistry);

    /// <summary>
    /// Registers a custom serializer adapter
    /// </summary>
    /// <param name="adapter">The adapter to register</param>
    void RegisterAdapter(ISerializerAdapter adapter);

    /// <summary>
    /// Registers a custom serializer adapter by type (must be registered in DI container)
    /// </summary>
    /// <typeparam name="T">The adapter type</typeparam>
    void RegisterAdapter<T>() where T : class, ISerializerAdapter;

    /// <summary>
    /// Gets all registered adapters
    /// </summary>
    /// <returns>All registered adapters</returns>
    IEnumerable<ISerializerAdapter> GetAllAdapters();

    /// <summary>
    /// Checks if an adapter exists for the specified serializer type
    /// </summary>
    /// <param name="serializerType">The serializer type</param>
    /// <returns>True if an adapter exists</returns>
    bool HasAdapterFor(SerializerType serializerType);
}