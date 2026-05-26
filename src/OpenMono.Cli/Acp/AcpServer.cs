using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace OpenMono.Acp;






public static class AcpServer
{
    public static WebApplication Build(AcpServerSettings settings, IServiceCollection services)
    {
        var builder = WebApplication.CreateBuilder();

        foreach (var d in services)
            builder.Services.Add(d);











        builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(settings.Port));

        builder.Services.AddSingleton(settings);

        var app = builder.Build();
        AcpEndpoints.Map(app);
        return app;
    }
}
