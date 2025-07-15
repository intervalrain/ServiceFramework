namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

/// <summary>
/// Generic request wrapper for primitive types in NATS communication
/// </summary>
/// <typeparam name="T">The primitive type to wrap</typeparam>
public class NatsRequest<T>
{
    public T Value { get; set; } = default!;

    public NatsRequest() { }

    public NatsRequest(T value)
    {
        Value = value;
    }

    public static implicit operator T(NatsRequest<T> request) => request.Value;
    public static implicit operator NatsRequest<T>(T value) => new(value);
}

/// <summary>
/// Specific request types for common primitive types
/// </summary>
public class NatsIdRequest : NatsRequest<Guid>
{
    public NatsIdRequest() { }
    public NatsIdRequest(Guid id) : base(id) { }
}

public class NatsStringRequest : NatsRequest<string>
{
    public NatsStringRequest() { }
    public NatsStringRequest(string value) : base(value) { }
}

public class NatsIntRequest : NatsRequest<int>
{
    public NatsIntRequest() { }
    public NatsIntRequest(int value) : base(value) { }
}

public class NatsLongRequest : NatsRequest<long>
{
    public NatsLongRequest() { }
    public NatsLongRequest(long value) : base(value) { }
}

public class NatsDoubleRequest : NatsRequest<double>
{
    public NatsDoubleRequest() { }
    public NatsDoubleRequest(double value) : base(value) { }
}


public class NatsBoolRequest : NatsRequest<bool>
{
    public NatsBoolRequest() { }
    public NatsBoolRequest(bool value) : base(value) { }
}