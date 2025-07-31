using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;

using ErrorOr;

namespace AuthorSystem.Application.Services;

public interface ISampleAppService : INatsService
{
    Task<ErrorOr<SampleOutput>> Square(SampleInput input);
    Task<ErrorOr<SampleOutput>> Sqrt(SampleInput input);
    Task<SampleOutput> Double(SampleInput input);
    Task<SampleOutput> Prime(SampleInput input);
}