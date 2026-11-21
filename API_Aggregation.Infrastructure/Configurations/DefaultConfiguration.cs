using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using API_Aggregation.Application.Middleware;
using System.Security.Claims;
using Serilog;

namespace API_Aggregation.Infrastructure.Configurations;

public static class DefaultConfiguration
{
    public static IServiceCollection AddDefaultConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        

        return services;
    }

    public static void UseCommonMiddleware(this WebApplication app)
    {
        app.UseMiddleware<ValidationExceptionMiddleware>();
        app.UseMiddleware<LogContextMiddleware>();
        app.UseSerilogRequestLogging(opts =>
        {
            opts.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("UserId", httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous");
                diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
            };
        });
    }
}

