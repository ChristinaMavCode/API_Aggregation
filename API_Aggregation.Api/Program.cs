using System.Text.Json;
using API_Aggregation.Api.Endpoints;
using API_Aggregation.Infrastructure.Configurations;
using Asp.Versioning;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

var isDevelopment = builder.Environment.IsDevelopment();

// Application + infrastructure (CQRS, external APIs, caching, statistics, background service)
builder.Services.AddDefaultConfiguration(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration, requireHttpsMetadata: !isDevelopment);
builder.Services.AddTelemetry("API_Aggregation-api", builder.Configuration);

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services
    .AddApiVersioning(o =>
    {
        o.DefaultApiVersion = new ApiVersion(1, 0);
        o.ReportApiVersions = true;
        o.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(o =>
    {
        o.GroupNameFormat = "'v'VVV";
        o.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddOpenApi("v1");

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5174"];
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (isDevelopment)
{
    app.MapOpenApi();   // /openapi/v1.json
}

app.UseCors();
app.UseCommonMiddleware();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health").ExcludeFromDescription();

var versionedApi = app.NewVersionedApi("API Aggregation");
var v1 = versionedApi
    .MapGroup("/api/v{version:apiVersion}")
    .HasApiVersion(1.0);

v1.MapAuthEndpoints();
v1.MapAggregationEndpoints();
v1.MapStatisticsEndpoints();

app.Run();

public partial class Program;
