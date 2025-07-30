using EdgeSync.ServiceFramework.AspNetCore.Mvc.Core;
using EdgeSync.ServiceFramework.Data;

using ErrorOr;

namespace AuthorSystem.Application.Services;

public interface ISampleAppService : INatsService
{
    Task<ResponseDto<SampleOutput>> Square(RequestDto<SampleInput> input);
    Task<ErrorOr<SampleOutput>> Sqrt(SampleInput input);
    Task<SampleOutput> Double(SampleInput input);
    Task<SampleOutput> Prime(SampleInput input);
}