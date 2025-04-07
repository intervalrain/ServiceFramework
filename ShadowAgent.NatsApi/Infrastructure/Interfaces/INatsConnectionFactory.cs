using System.Threading;
using System.Threading.Tasks;

using NATS.Client.Core;

namespace ShadowAgent.Infrastructure.Interfaces;

public interface INatsConnectionFactory
{
    Task<INatsConnection> CreateConnectionAsync(string url ="", string credFile ="",
                                        CancellationToken cancellationToken = default);
}