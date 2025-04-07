using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NATS.Client.Core;
using NATS.Client.KeyValueStore;

using ShadowAgent.Infrastructure.Interfaces;


namespace ShadowAgent.Infrastructure.Nats
{
public class KvStore : MsgBusJetStreamClient, IKvStore
{
    private Dictionary<string, INatsKVStore> _kvStores = new Dictionary<string, INatsKVStore>();

    public KvStore(ILogger<KvStore> logger, INatsConnectionFactory natsConnectionFactory): base(logger, natsConnectionFactory)
    {
        base.TryConnectAsync().Wait();
    }

    public async Task<INatsKVStore> GetKvStore(string bucket)
    {
        if (_kvStores.ContainsKey(bucket))
        {
            return _kvStores[bucket];
        }
        var kvStore = await base.CreateKeyValueStore(bucket);
        _kvStores[bucket] = kvStore;
        return kvStore;
    }

    public async Task SetValueAsync(string bucket, string key, byte[] value)
    {
        var kv = await GetKvStore(bucket);
        await kv.PutAsync(key, value);
    }

    /// Asynchronously sets a value in the specified bucket with the given key.
    /// </summary>
    /// <typeparam name="T">The type of the value to be stored.</typeparam>
    /// <param name="bucket">The name of the bucket where the value will be stored.</param>
    /// <param name="key">The key under which the value will be stored.</param>
    /// <param name="value">The value to be stored.</param>
    /// <param name="serializer">An optional serializer to use for the value. If not provided, a default serializer will be used.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SetValueAsync<T>(string bucket, string key, T value, INatsSerialize<T>? serializer = null)
    {
        var kv = await GetKvStore(bucket);
        if (serializer == null)
        {
            await kv.PutAsync(key, JsonSerializer.SerializeToUtf8Bytes(value));
        }
        else 
        {
            await kv.PutAsync(key, value, serializer);
        }
        
    }

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
    public async Task<T?> GetValueAsync<T>(string bucket, string key, ulong revision = 0uL,
                                        INatsDeserialize<T>? serializer = null,
                                        CancellationToken cancellationToken = default(CancellationToken))
    {
        var kv = await GetKvStore(bucket);
        var val = await kv.GetEntryAsync<T>(key, revision, serializer, cancellationToken);

        if (val == default)
        {
            return default;
        }
        return val.Value;
    }
}

    internal class ConnectionFactory
    {
        public ConnectionFactory()
        {
        }
    }
}