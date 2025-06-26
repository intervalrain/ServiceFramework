namespace EdgeSync.ServiceFramework.AspNetCore.Mvc.Models;

public class NatsResponse<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
}