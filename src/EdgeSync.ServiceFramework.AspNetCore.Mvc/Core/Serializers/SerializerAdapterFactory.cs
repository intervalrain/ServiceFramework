using NATS.Client.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EdgeSync.ServiceFramework.Core.Serializers;

/// <summary>
/// Factory for creating and managing serializer adapters.
/// Provides a registry of adapters and automatically selects the appropriate adapter for a given serializer.
/// </summary>
public class SerializerAdapterFactory : ISerializerAdapterFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SerializerAdapterFactory> _logger;
    private readonly List<ISerializerAdapter> _adapters;
    private readonly Dictionary<string, ISerializerAdapter> _adapterCache;

    public SerializerAdapterFactory(IServiceProvider serviceProvider, ILogger<SerializerAdapterFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _adapters = [];
        _adapterCache = [];
        
        // Register built-in adapters
        RegisterBuiltInAdapters();
    }

    public ISerializerAdapter GetAdapter(INatsSerializerRegistry serializerRegistry)
    {
        var registryTypeName = serializerRegistry.GetType().FullName ?? "Unknown";
        
        // Check cache first
        if (_adapterCache.TryGetValue(registryTypeName, out var cachedAdapter))
        {
            return cachedAdapter;
        }

        // Find appropriate adapter
        foreach (var adapter in _adapters)
        {
            if (adapter.CanAdapt(serializerRegistry))
            {
                _logger.LogDebug("Selected {AdapterType} for serializer registry {RegistryType}", 
                    adapter.GetType().Name, registryTypeName);
                    
                // Cache the result
                _adapterCache[registryTypeName] = adapter;
                return adapter;
            }
        }

        // Fallback to JSON adapter if no specific adapter found
        var fallbackAdapter = _adapters.OfType<JsonSerializerAdapter>().FirstOrDefault();
        if (fallbackAdapter != null)
        {
            _logger.LogWarning("No specific adapter found for serializer registry {RegistryType}, falling back to JSON adapter", 
                registryTypeName);
            _adapterCache[registryTypeName] = fallbackAdapter;
            return fallbackAdapter;
        }

        throw new InvalidOperationException($"No suitable serializer adapter found for registry type: {registryTypeName}");
    }

    public ITypedSerializerAdapter GetTypedAdapter(INatsSerializerRegistry serializerRegistry)
    {
        var adapter = GetAdapter(serializerRegistry);
        
        if (adapter is ITypedSerializerAdapter typedAdapter)
        {
            return typedAdapter;
        }

        // If the adapter doesn't implement ITypedSerializerAdapter, throw an exception
        throw new InvalidOperationException(
            $"Adapter {adapter.GetType().Name} does not support typed operations. " +
            "Please use an adapter that implements ITypedSerializerAdapter for type-safe operations.");
    }

    public void RegisterAdapter(ISerializerAdapter adapter)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        
        _adapters.Add(adapter);
        _logger.LogInformation("Registered custom serializer adapter: {AdapterType} for {SerializerType}", 
            adapter.GetType().Name, adapter.SerializerType);
        
        // Clear cache to force re-evaluation
        _adapterCache.Clear();
    }

    public void RegisterAdapter<T>() where T : class, ISerializerAdapter
    {
        var adapter = _serviceProvider.GetService<T>();
        if (adapter == null)
        {
            throw new InvalidOperationException($"Adapter type {typeof(T).Name} is not registered in the service container");
        }
        
        RegisterAdapter(adapter);
    }

    public IEnumerable<ISerializerAdapter> GetAllAdapters()
    {
        return _adapters.AsReadOnly();
    }

    public bool HasAdapterFor(SerializerType serializerType)
    {
        return _adapters.Any(a => a.SerializerType == serializerType);
    }

    private void RegisterBuiltInAdapters()
    {
        // Register JSON adapter (enhanced with type safety)
        var jsonAdapter = new JsonSerializerAdapter(_serviceProvider.GetRequiredService<ILogger<JsonSerializerAdapter>>());
        _adapters.Add(jsonAdapter);

        // TODO: Register legacy Protobuf adapter for backward compatibility        

        _logger.LogInformation("Registered built-in serializer adapters: JSON ");
    }
}