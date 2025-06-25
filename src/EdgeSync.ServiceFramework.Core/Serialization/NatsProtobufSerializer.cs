using System.Buffers;

using Google.Protobuf;

using NATS.Client.Core;

namespace EdgeSync.ServiceFramework.Core.Serialization;

public class NatsProtobufSerializer<T> : INatsSerializer<T> where T : IMessage, new()
{
    private readonly INatsSerializer<T>? _next;    

    public NatsProtobufSerializer() : this(null)
    {
    }

    public NatsProtobufSerializer(INatsSerializer<T>? next = null)
    {
        _next = next;
    }

    public void Serialize(IBufferWriter<byte> bufferWriter, T value)
    {
        if (value is IMessage message)
        {
            message.WriteTo(bufferWriter);
        }
        else if (_next != null)
        {
            _next.Serialize(bufferWriter, value);
        }
        else 
        {
            throw new NatsException($"Cannot serialize type {typeof(T)}");
        }
    }

    public T? Deserialize(in ReadOnlySequence<byte> buffer)
    {
        try
        {
            var message = new T();

            if (buffer.IsSingleSegment)
            {
                message.MergeFrom(buffer.First.Span);
            }
            else 
            {
                byte[] bytes = buffer.ToArray();
                message.MergeFrom(bytes);
            }

            return message;
        }
        catch (Exception) when (_next != null)
        {
            return _next.Deserialize(buffer);
        }
    }

    public INatsSerializer<T> CombineWith(INatsSerializer<T> next)
    {
        return new NatsProtobufSerializer<T>(next);
    }
}