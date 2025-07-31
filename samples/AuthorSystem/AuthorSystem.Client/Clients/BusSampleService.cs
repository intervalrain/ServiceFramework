using System.Text.Json;

using AuthorSystem.Client.Models;
using EdgeSync.ServiceFramework.Abstractions.JetStream;
using EdgeSync.ServiceFramework.Data;
using Microsoft.Extensions.Logging;

namespace AuthorSystem.Client.Clients;

public class BusSampleService : ISampleService
{
    private readonly ILogger<BusSampleService> _logger;
    private readonly IJetStreamClient _bus;

    public BusSampleService(ILogger<BusSampleService> logger, IJetStreamClientFactory factory)
    {
        _logger = logger;
        _bus = factory.CreateClient("bus");
    }

    public async Task<int> Double(int num) => await RequestAsync("double", new NumberInput { Number = num });
    public async Task<bool> Prime(int num) => await RequestAsync("prime", new NumberInput { Number = num }) == 1;
    public async Task<int> Sqrt(int num) => await RequestAsync("sqrt", new NumberInput { Number = num });
    public async Task<int> Square(int num) => await RequestAsync("square", new NumberInput { Number = num });

    private async Task<int> RequestAsync(string subject, NumberInput request)
    {
        try
        {
            var result = await _bus.RequestAsync<NumberInput, ResponseDto<NumberInput>>(subject, request);
            if (result != null)
            {
                return result?.Data?.Number ?? 0;
            }
            else
            {
                _logger.LogWarning("No response received");
            }
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Request timed out");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending request");
        }
        return 0;
    }
}