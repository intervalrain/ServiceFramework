# 序列化適配器架構改進方案

## 問題分析

### 原有架構的問題

#### 1. JsonSerializerAdapter 類型安全問題
```csharp
// 問題：使用 object 類型失去編譯時類型安全
var response = await connection.RequestAsync<object, object>(subject, request,
    requestSerializer: serializerRegistry.GetSerializer<object>(),
    replySerializer: serializerRegistry.GetDeserializer<object>());
```

**問題根因：**
- 雖然知道 `expectedResponseType`，但沒有利用泛型來實現類型安全
- 運行時類型轉換可能失敗，且不易發現
- IntelliSense 支持差，開發體驗不佳

#### 2. ProtobufSerializerAdapter IMessage 約束問題
```csharp
// 問題：ErrorOr<List<BookDto>> 無法直接映射到 IMessage
public async Task<ErrorOr<List<BookDto>>> GetBooksAsync()
```

**問題根因：**
- Protobuf 要求所有類型實現 `IMessage` 接口
- `ErrorOr<T>` 和 `List<T>` 等 .NET 泛型類型不是 Protobuf 消息
- 缺乏動態類型映射機制

#### 3. 架構設計缺陷
- 缺乏類型安全的泛型接口
- 適配器無法充分利用反射和泛型
- 對複雜泛型類型缺乏統一的處理機制

## 解決方案架構

### 1. 增強的類型安全接口

#### ITypedSerializerAdapter
```csharp
public interface ITypedSerializerAdapter : ISerializerAdapter
{
    Task<TResponse> SendRequestAsync<TRequest, TResponse>(
        INatsConnection connection,
        string subject,
        TRequest request,
        INatsSerializerRegistry serializerRegistry);

    Task<TResponse> SendParameterlessRequestAsync<TResponse>(
        INatsConnection connection,
        string subject,
        INatsSerializerRegistry serializerRegistry);

    bool CanHandleTypes(Type? requestType, Type? responseType);
    TypedSerializationConfig GetTypedConfig(Type? requestType, Type? responseType, INatsSerializerRegistry serializerRegistry);
}
```

**優勢：**
- 編譯時類型安全
- 更好的 IntelliSense 支持
- 減少運行時類型轉換錯誤

### 2. JsonSerializerAdapter 改進

#### 類型安全實現
```csharp
public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
    INatsConnection connection,
    string subject,
    TRequest request,
    INatsSerializerRegistry serializerRegistry)
{
    // 直接使用泛型類型，無需 object 轉換
    var response = await connection.RequestAsync<TRequest, TResponse>(subject, request,
        requestSerializer: serializerRegistry.GetSerializer<TRequest>(),
        replySerializer: serializerRegistry.GetDeserializer<TResponse>());
    
    return response.Data;
}
```

**改進效果：**
- ✅ 支持 `RequestAsync<Guid, ErrorOr<BookDto>>`
- ✅ 支持 `RequestAsync<CreateBookDto, ErrorOr<BookDto>>`
- ✅ 支持 `RequestAsync<object, ErrorOr<List<BookDto>>>`
- ✅ 編譯時類型檢查

### 3. EnhancedProtobufSerializerAdapter 解決方案

#### UniversalMessage 統一處理
```csharp
public async Task<TResponse> SendRequestAsync<TRequest, TResponse>(
    INatsConnection connection,
    string subject,
    TRequest request,
    INatsSerializerRegistry serializerRegistry)
{
    // 將 .NET 對象轉換為 UniversalMessage
    var requestMessage = UniversalProtobufConverter.ToUniversalMessage(request);

    // 使用 UniversalMessage 進行 Protobuf 通信
    var response = await connection.RequestAsync<UniversalMessage, UniversalMessage>(subject, requestMessage,
        requestSerializer: serializerRegistry.GetSerializer<UniversalMessage>(),
        replySerializer: serializerRegistry.GetDeserializer<UniversalMessage>());

    // 將 UniversalMessage 轉回目標類型
    var result = UniversalProtobufConverter.FromUniversalMessage(response.Data, typeof(TResponse));
    return (TResponse)result!;
}
```

**解決的問題：**
- ✅ `ErrorOr<List<BookDto>>` 可以通過 UniversalMessage 序列化
- ✅ 保持 Protobuf 的 IMessage 約束
- ✅ 支持任意 .NET 類型

### 4. 動態類型映射系統

#### DynamicProtobufTypeMapper
```csharp
public class DynamicProtobufTypeMapper
{
    public ProtobufTypeMapping GetProtobufMapping(Type dotnetType)
    {
        // ErrorOr<T> -> ErrorOrResult
        if (IsErrorOrType(dotnetType))
            return MapErrorOrType(dotnetType);

        // List<T> -> CollectionResult
        if (IsCollectionType(dotnetType))
            return MapCollectionType(dotnetType);

        // 其他類型 -> UniversalMessage
        return new ProtobufTypeMapping(
            dotnetType, 
            typeof(UniversalMessage), 
            obj => UniversalProtobufConverter.ToUniversalMessage(obj));
    }
}
```

**映射策略：**
- `ErrorOr<BookDto>` → `ErrorOrResult` (使用專用 Protobuf 消息)
- `List<BookDto>` → `CollectionResult` (使用集合包裝器)
- `ErrorOr<List<BookDto>>` → `UniversalMessage` (使用通用包裝器)

## 使用示例

### 1. JSON 序列化器的類型安全使用

```csharp
// 之前：類型不安全
var response = await adapter.SendRequestAsync(connection, "book.get", bookId, false, typeof(ErrorOr<BookDto>), registry);
var result = (ErrorOr<BookDto>)response; // 可能運行時失敗

// 現在：類型安全
var result = await typedAdapter.SendRequestAsync<Guid, ErrorOr<BookDto>>(connection, "book.get", bookId, registry);
```

### 2. Protobuf 序列化器處理複雜類型

```csharp
// ErrorOr<List<BookDto>> 現在可以正常工作
var books = await typedAdapter.SendParameterlessRequestAsync<ErrorOr<List<BookDto>>>(
    connection, "book.list", registry);

// 內部自動處理：
// 1. 創建 Empty 消息
// 2. 接收 UniversalMessage
// 3. 轉換為 ErrorOr<List<BookDto>>
```

### 3. 適配器選擇策略

```csharp
public async Task<TResponse> SmartRequestAsync<TRequest, TResponse>(
    string subject, TRequest request)
{
    // 檢查類型兼容性並選擇最佳適配器
    foreach (var registry in GetSerializerRegistries())
    {
        var adapter = _factory.GetTypedAdapter(registry);
        if (adapter.CanHandleTypes(typeof(TRequest), typeof(TResponse)))
        {
            return await adapter.SendRequestAsync<TRequest, TResponse>(
                _connection, subject, request, registry);
        }
    }
    throw new InvalidOperationException("No suitable adapter found");
}
```

## 架構最佳實踐

### 1. 類型安全原則

#### DO ✅
```csharp
// 使用具體泛型類型
Task<ErrorOr<BookDto>> GetBookAsync(Guid id);

// 檢查適配器兼容性
if (adapter.CanHandleTypes(typeof(TRequest), typeof(TResponse)))
{
    return await adapter.SendRequestAsync<TRequest, TResponse>(...);
}
```

#### DON'T ❌
```csharp
// 避免使用 object 類型
Task<object> GetBookAsync(object id);

// 避免強制類型轉換
var result = (ErrorOr<BookDto>)response;
```

### 2. 適配器選擇策略

```csharp
public static class AdapterSelectionStrategy
{
    public static ITypedSerializerAdapter SelectAdapter(
        ISerializerAdapterFactory factory,
        Type? requestType,
        Type? responseType)
    {
        // 1. JSON 優先（最兼容）
        if (CanUseJsonAdapter(requestType, responseType))
            return factory.GetTypedAdapter(JsonRegistry);

        // 2. Protobuf（性能優化）
        if (CanUseProtobufAdapter(requestType, responseType))
            return factory.GetTypedAdapter(ProtobufRegistry);

        // 3. 自定義適配器
        return GetCustomAdapter(factory, requestType, responseType);
    }
}
```

### 3. 錯誤處理模式

```csharp
public static async Task<ErrorOr<T>> SafeRequestAsync<T>(
    ITypedSerializerAdapter adapter,
    INatsConnection connection,
    string subject,
    INatsSerializerRegistry registry)
{
    try
    {
        return await adapter.SendParameterlessRequestAsync<ErrorOr<T>>(
            connection, subject, registry);
    }
    catch (Exception ex)
    {
        return Error.Failure("NATS_ERROR", $"Communication failed: {ex.Message}");
    }
}
```

### 4. 性能優化策略

#### 適配器緩存
```csharp
private readonly ConcurrentDictionary<string, ITypedSerializerAdapter> _adapterCache = new();

public ITypedSerializerAdapter GetCachedAdapter(Type requestType, Type responseType)
{
    var key = $"{requestType?.FullName}:{responseType?.FullName}";
    return _adapterCache.GetOrAdd(key, _ => SelectBestAdapter(requestType, responseType));
}
```

#### 序列化配置重用
```csharp
private readonly ConcurrentDictionary<Type, TypedSerializationConfig> _configCache = new();

public TypedSerializationConfig GetCachedConfig(Type type, INatsSerializerRegistry registry)
{
    return _configCache.GetOrAdd(type, t => adapter.GetTypedConfig(t, null, registry));
}
```

## 遷移指南

### 階段 1：向後兼容
1. 保持現有 `ISerializerAdapter` 接口不變
2. 新增 `ITypedSerializerAdapter` 接口
3. 現有適配器實現新接口

### 階段 2：逐步遷移
1. 新代碼使用 `ITypedSerializerAdapter`
2. 舊代碼逐步遷移到類型安全版本
3. 添加適配器選擇邏輯

### 階段 3：優化完善
1. 移除 object 類型的使用
2. 優化性能和緩存
3. 完善錯誤處理

## 總結

### 改進成果

1. **JsonSerializerAdapter**：
   - ✅ 支持 `RequestAsync<TRequest, TResponse>`
   - ✅ 編譯時類型安全
   - ✅ 更好的開發體驗

2. **ProtobufSerializerAdapter**：
   - ✅ 支持 `ErrorOr<List<BookDto>>` 等複雜類型
   - ✅ 動態類型映射到 IMessage
   - ✅ UniversalMessage 統一處理

3. **架構設計**：
   - ✅ 類型安全的泛型接口
   - ✅ 靈活的適配器選擇機制
   - ✅ 向後兼容性

### 技術價值

- **類型安全**：減少運行時錯誤，提高代碼質量
- **開發體驗**：更好的 IntelliSense 和編譯時檢查
- **可維護性**：清晰的接口設計和錯誤處理
- **可擴展性**：支持自定義適配器和映射策略
- **性能**：優化的緩存機制和類型轉換

這個改進方案解決了原有架構的核心問題，提供了類型安全、性能優化且易於維護的序列化解決方案。