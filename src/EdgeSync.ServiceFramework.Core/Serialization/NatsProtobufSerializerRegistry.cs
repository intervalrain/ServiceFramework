using Google.Protobuf;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Core.Serialization;

public class NatsProtobufSerializerRegistry : INatsSerializerRegistry
{
    private readonly INatsSerializerRegistry? _fallbackRegistry;
    public static readonly NatsProtobufSerializerRegistry Default = new(NatsDefaultSerializerRegistry.Default);

    public NatsProtobufSerializerRegistry(INatsSerializerRegistry? fallbackRegistry = null)
    {
        _fallbackRegistry = fallbackRegistry;
    }

    public INatsDeserialize<T> GetDeserializer<T>()
    {
        if (typeof(IMessage).IsAssignableFrom(typeof(T)) && !typeof(T).IsAbstract && typeof(T).GetConstructor(Type.EmptyTypes) != null)
        {
            var serializerType = typeof(NatsProtobufSerializer<>).MakeGenericType(typeof(T));
            INatsDeserialize<T> deserializer = (INatsDeserialize<T>)Activator.CreateInstance(serializerType)!;

            if (_fallbackRegistry != null)
            {
                try
                {
                    var fallbackDeserializer = _fallbackRegistry.GetDeserializer<T>();
                    if (deserializer is INatsSerializer<T> natsDeserializer && fallbackDeserializer is INatsSerializer<T> fallbackNatsDeserializer)
                    {
                        return natsDeserializer.CombineWith(fallbackNatsDeserializer);
                    }
                }
                catch
                {
                }
            }

            return deserializer;
        }

        if (_fallbackRegistry != null)
        {
            return _fallbackRegistry.GetDeserializer<T>();
        }

        throw new NatsException($"Type {typeof(T)} is not a valid protobuf message type, and no serializer supported.");
    }

    public INatsSerialize<T> GetSerializer<T>()
    {
        if (typeof(IMessage).IsAssignableFrom(typeof(T)) && !typeof(T).IsAbstract && typeof(T).GetConstructor(Type.EmptyTypes) != null)
        {
            var serializerType = typeof(NatsProtobufSerializer<>).MakeGenericType(typeof(T));
            INatsSerialize<T> serializer = (INatsSerialize<T>)Activator.CreateInstance(serializerType)!;

            if (_fallbackRegistry != null)
            {
                try
                {
                    var fallbackSerializer = _fallbackRegistry.GetSerializer<T>();
                    if (serializer is INatsSerializer<T> natsSerializer && fallbackSerializer is INatsSerializer<T> fallbackNatsSerializer)
                    {
                        return natsSerializer.CombineWith(fallbackNatsSerializer);
                    }
                }
                catch
                {
                }
            }

            return serializer;
        }

        if (_fallbackRegistry != null)
        {
            return _fallbackRegistry.GetSerializer<T>();
        }

        throw new NatsException($"Type {typeof(T)} is not a valid protobuf message type, and no serializer supported.");

    }
}