using System.Text;

using EdgeSync.ServiceFramework.JetStream;

using NATS.Client.Core;
using NATS.Client.JetStream;

namespace EdgeSync.ServiceFramework.Testlib;

public abstract class MockJetStreamClient : IJetStreamClient
{
    private readonly List<NatsMessage> _publishedMessages = [];
    public IReadOnlyList<NatsMessage> PublishedMessages => _publishedMessages.AsReadOnly();

    /// <summary>
    /// Mock NATS connection for testing
    /// </summary>
    public INatsConnection NatsConnection { get; } = null!; // Mock implementation
    public bool PublishWasCalled { get; private set; }
    public string? LastPublishedSubject { get; private set; }
    public byte[]? LastPublishedData { get; private set; }

    private readonly List<NatsMessage> _requestedMessages = [];
    public IReadOnlyList<NatsMessage> RequestedMessages => _requestedMessages.AsReadOnly();
    public bool RequestWasCalled { get; private set; }
    public string? LastRequestedSubject { get; private set; }
    public byte[]? LastRequestedData { get; private set; }

    public int MaxMsgs => 100;

    private readonly Dictionary<string, List<Func<string, byte[], bool>>> _subjectVerifiers = [];

    /// <summary>
    /// 添加自定義主題驗證器
    /// </summary>
    public void AddVerifier(string subject, Func<string, byte[], bool> verifier)
    {
        if (!_subjectVerifiers.TryGetValue(subject, out var value))
        {
            value = [];
            _subjectVerifiers[subject] = value;
        }

        value.Add(verifier);
    }

    /// <summary>
    /// 驗證是否發布到指定主題
    /// </summary>
    public bool VerifyPublishToSubject(string subject)
    {
        return _publishedMessages.Exists(m => m.Subject == subject);
    }

    /// <summary>
    /// 驗證消息內容是否包含特定字符串
    /// </summary>
    public bool VerifyMessageContains(string subject, string expectedContent)
    {
        foreach (var message in _publishedMessages)
        {
            if (message.Subject == subject)
            {
                string content = Encoding.UTF8.GetString(message.Data);
                if (content.Contains(expectedContent))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    /// <summary>
    /// 獲取發布到特定主題的消息數量
    /// </summary>
    public int GetPublishCount(string subject)
    {
        return _publishedMessages.Count(m => m.Subject == subject);
    }

    /// <summary>
    /// 獲取特定主題的最後一條消息
    /// </summary>
    public NatsMessage GetLastMessage(string subject)
    {
        return _publishedMessages.FindLast(m => m.Subject == subject) ?? new NatsMessage("No Message", []);
    }

    /// <summary>
    /// 清除所有記錄的發布消息
    /// </summary>
    public void ClearMessages()
    {
        _publishedMessages.Clear();
        PublishWasCalled = false;
        LastPublishedSubject = null;
        LastPublishedData = null;
    }

    /// <summary>
    /// 實現 IJetStreamClient 的方法，記錄發布的消息
    /// </summary>
    public virtual Task PublishAsync<T>(string subject, T? data, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        PublishWasCalled = true;
        LastPublishedSubject = subject;
        
        // 將數據轉換為 byte[]
        byte[] byteData;
        if (data is byte[] bytes)
        {
            byteData = bytes;
        }
        else if (data is string str)
        {
            byteData = Encoding.UTF8.GetBytes(str);
        }
        else if (data == null)
        {
            byteData = [];
        }
        else
        {
            // 默認轉換為 JSON
            byteData = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(data));
        }
        
        LastPublishedData = byteData;
        
        // 記錄消息
        var message = new NatsMessage(subject, byteData);
        _publishedMessages.Add(message);
        
        // 執行驗證器
        if (_subjectVerifiers.TryGetValue(subject, out var verifiers))
        {
            foreach (var verifier in verifiers)
            {
                if (!verifier(subject, byteData))
                {
                    throw new VerificationException($"驗證失敗: 主題 {subject} 的消息未通過自定義驗證");
                }
            }
        }
        
        return Task.CompletedTask;
    }

    // IJetStreamClient 接口的其他方法實現
    public virtual NatsOpts ClientOpts(NatsOpts opts)
    {
        throw new NotImplementedException();
    }

    public virtual void Connect()
    {
        // 默認實現，子類可以根據需要覆寫
    }

    public virtual Task ConsumeAsync(INatsJSConsumer consumer, Func<byte[], string, Task> handler, bool autoAck = true, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public virtual Task<INatsJSConsumer> CreateStreamConsumerAsync(string consumerName, string streamName, string subject)
    {
        throw new NotImplementedException();
    }

    public virtual Task<INatsJSConsumer> CreateStreamConsumerAsync(ConsumerConfigOptions consumerCfg, JetStreamConfigOptions cfgOptions)
    {
        throw new NotImplementedException();
    }

    public virtual void Disconnect()
    {
        // 默認實現，子類可以根據需要覆寫
    }

    public virtual void Dispose()
    {
        // 默認實現，子類可以根據需要覆寫
    }

    public virtual bool IsConnected()
    {
        return true; // 默認總是返回已連接
    }

    public virtual Task NatsPublishAsync<T>(string subject, T? data, INatsSerialize<T>? serializer = null, CancellationToken cancellationToken = default)
    {
        // 默認調用 PublishAsync 實現
        return PublishAsync(subject, data, serializer, cancellationToken);
    }

    public Task TryConnectAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task<string?> RequestAsync<T>(string subject, T data, CancellationToken cancellationToken = default)
    {
        RequestWasCalled = true;
        LastRequestedSubject = subject;
        
        // 將數據轉換為 byte[]
        byte[] byteData;
        if (data is byte[] bytes)
        {
            byteData = bytes;
        }
        else if (data is string str)
        {
            byteData = Encoding.UTF8.GetBytes(str);
        }
        else if (data == null)
        {
            byteData = [];
        }
        else
        {
            // 默認轉換為 JSON
            byteData = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(data));
        }
        
        LastRequestedData = byteData;
        
        // 記錄消息
        var message = new NatsMessage(subject, byteData);
        _requestedMessages.Add(message);
        
        // 執行驗證器
        if (_subjectVerifiers.TryGetValue(subject, out var verifiers))
        {
            foreach (var verifier in verifiers)
            {
                if (!verifier(subject, byteData))
                {
                    throw new VerificationException($"驗證失敗: 主題 {subject} 的消息未通過自定義驗證");
                }
            }
        }
        
        return Task.FromResult("This is a mock reply")!;
    }
}

/// <summary>
/// 發布消息記錄類
/// </summary>
public class NatsMessage(string subject, byte[] data)
{
    public string Subject { get; } = subject;
    public byte[] Data { get; } = data;
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    public string GetPayloadAsString() => Encoding.UTF8.GetString(Data);
}

/// <summary>
/// 驗證失敗異常
/// </summary>
public class VerificationException(string message) : Exception(message)
{
}