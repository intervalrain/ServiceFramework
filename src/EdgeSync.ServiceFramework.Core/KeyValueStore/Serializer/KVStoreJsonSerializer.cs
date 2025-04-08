using System.Buffers;
using System.Text.Json;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.KeyValueStore.Serializer;

public class KVStoreJsonSerializer<T> : INatsDeserialize<T>
{
    public T? Deserialize(byte[] data)
    {
        return JsonSerializer.Deserialize<T>(data);
    }

    public T? Deserialize(in ReadOnlySequence<byte> buffer)
    {
        var reader = new Utf8JsonReader(buffer);
        return JsonSerializer.Deserialize<T>(ref reader);
    }
}