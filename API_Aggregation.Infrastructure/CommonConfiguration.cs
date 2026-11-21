using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StartUp.Application.Interface;
using StartUp.Application.Services;
using StartUp.Infrastructure;

namespace TMC_ERP_Common.Infrastructure;

public static class CommonConfiguration
{
    public static IServiceCollection AddCommonConfiguration(this IServiceCollection services, string? connectionString)
    {
        void ConfigureSql(DbContextOptionsBuilder options) =>
        options.UseSqlServer(connectionString,
            sql =>
            {
                sql.EnableRetryOnFailure();
                sql.CommandTimeout(30);
            });
        services.AddDbContextFactory<LoginCtx>(ConfigureSql, ServiceLifetime.Transient);

        services.AddSignalR();

        services.AddScoped<ILoginRepository, LoginRepository>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}