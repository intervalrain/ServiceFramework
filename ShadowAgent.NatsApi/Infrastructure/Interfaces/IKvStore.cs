using System.Buffers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NATS.Client.Core;

namespace ShadowAgent.Infrastructure.Nats
{
    public class KvStoreDeserializer<T>: INatsDeserialize<T>
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

    public interface IKvStore
    {
        Task SetValueAsync(string bucket, string key, byte[] value);
        /// Asynchronously sets a value in the specified bucket with the given key.
        /// </summary>
        /// <typeparam name="T">The type of the value to be stored.</typeparam>
        /// <param name="bucket">The name of the bucket where the value will be stored.</param>
        /// <param name="key">The key under which the value will be stored.</param>
        /// <param name="value">The value to be stored.</param>
        /// <param name="serializer">An optional serializer to use for the value. If not provided, a default serializer will be used.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task SetValueAsync<T>(string bucket, string key, T value, INatsSerialize<T>? serializer = null);
        /// <summary>
        /// Retrieves a value from the specified bucket and key in the KV store asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the value to retrieve.</typeparam>
        /// <param name="bucket">The name of the bucket in the KV store.</param>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <param name="revision">The revision number of the value to retrieve. Default is 0.</param>
        /// <param name="serializer">An optional serializer to use for deserializing the value.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the value of type <typeparamref name="T"/> if found; otherwise, default value of <typeparamref name="T"/>.</returns>
        Task<T?> GetValueAsync<T>(string bucket, string key, ulong revision = 0uL,
                                INatsDeserialize<T>? serializer = null,
                                CancellationToken cancellationToken = default(CancellationToken));
    }
}