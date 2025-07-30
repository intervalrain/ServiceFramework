using EdgeSync.ServiceFramework.Abstractions.Attributes;
using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.Attributes;
using EdgeSync.ServiceFramework.Data;

using ErrorOr;

using Microsoft.Extensions.Logging;

namespace AuthorSystem.Application.Services;

[Channel("bus")]
[ServiceInfo(ServiceName = "Sample", ServiceVersion = "1.0.0", QueueGroup = "sample-q")]
public class SampleAppService(ILogger<SampleAppService> logger)
    : NatsService(logger), ISampleAppService
{
    // Enwrap RequestDto & ResponseDto by yourself
    // you need to enrich audit info by using `EnrichWith`
    [Subject("square", "square")]
    public Task<ResponseDto<SampleOutput>> Square(RequestDto<SampleInput> input)
    {
        var result = input.Data.Number * input.Data.Number;
        var output = new SampleOutput(result);
        var response = ResponseDto<SampleOutput>.Success(output).EnrichWith(input);
        return Task.FromResult(response);
    }

    [Subject("sqrt", "sqrt")]
    // Using ErrorOr pattern without RequestDto and ResponseDto
    public Task<ErrorOr<SampleOutput>> Sqrt(SampleInput input)
    {
        if (input.Number < 0)
        {
            return Task.FromResult<ErrorOr<SampleOutput>>(Error.Failure("Number.Invalid", "Negative Number is not allowed")
        );
        }
        var result = Math.Sqrt(input.Number);
        var output = new SampleOutput(result);
        return Task.FromResult(output.ToErrorOr());
    }

    [Subject("double", "double")]
    public Task<SampleOutput> Double(SampleInput input)
    {
        var result = input.Number + input.Number;
        var output = new SampleOutput(result);
        return Task.FromResult(output);
    }

    [Subject("prime", "prime")]
    public Task<SampleOutput> Prime(SampleInput input)
    {
        if (input.Number < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(input.Number), "Negative numbers cannot be prime.");
        }

        var number = (int)Math.Floor(input.Number);
        var result = IsPrime(number) ? 1 : 0;
        var output = new SampleOutput(result);
        return Task.FromResult(output);
    }

    private static bool IsPrime(int n)
    {
        if (n <= 1) return false;
        if (n == 2) return true;
        if (n % 2 == 0) return false;
        var limit = (int)Math.Sqrt(n);
        for (int i = 3; i <= limit; i += 2)
        {
            if (n % i == 0) return false;
        }
        return true;
    }
}

public record SampleInput(double Number);
public record SampleOutput(double Number);