using Microsoft.Extensions.DependencyInjection;

namespace ManicDigger.Worker;

// Thin wrapper — makes the lazy intent visible in the constructor signature.
public sealed class WorkerHostAccessor
{
    private readonly IServiceProvider _sp;
    public WorkerHostAccessor(IServiceProvider sp) => _sp = sp;
    public WorkerHost Get() => _sp.GetRequiredService<WorkerHost>();
    public Server GetServer() => _sp.GetRequiredService<Server>();
}