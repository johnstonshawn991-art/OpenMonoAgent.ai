using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OpenMono.Acp;

















public sealed class AcpHostedService : IHostedService, IAsyncDisposable
{
    private readonly AcpServerSettings _settings;
    private readonly IServiceCollection _services;
    private readonly AcpLockFileWriter? _lockfile;
    private WebApplication? _app;

    public AcpHostedService(
        AcpServerSettings settings,
        IServiceCollection services,
        AcpLockFileWriter? lockfile = null)
    {
        _settings = settings;
        _services = services;
        _lockfile = lockfile;
    }


    public WebApplication App => _app
        ?? throw new InvalidOperationException("AcpHostedService has not been started.");

    public async Task StartAsync(CancellationToken ct)
    {
        if (_app is not null) return;
        _app = AcpServer.Build(_settings, _services);
        await _app.StartAsync(ct);
        _lockfile?.Write();
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _lockfile?.TryRemove();
        if (_app is not null)
        {
            await _app.StopAsync(ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.DisposeAsync();
            _app = null;
        }
    }
}
