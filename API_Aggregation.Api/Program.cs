using System.Text.Json;
using API_Aggregation.Application.CQRS.Commands.GenerateToken;
using API_Aggregation.Application.CQRS.Queries.GetAggregatedData;
using API_Aggregation.Application.CQRS.Queries.GetApiStatistics;
using API_Aggregation.Application.Dtos;
using API_Aggregation.Infrastructure.Configurations;
using Asp.Versioning;
using MediatR;
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

// Authentication
v1.MapPost("/auth/token", async (GenerateTokenCommand command, ISender sender, CancellationToken cancellationToken) =>
{
    var token = await sender.Send(command, cancellationToken);
    return token is not null
        ? Results.Ok(token)
        : Results.Problem(title: "Invalid username or password.", statusCode: StatusCodes.Status401Unauthorized);
}).WithName("CreateToken")
    .WithTags("Authentication")
    .WithSummary("Obtain a JWT access token")
    .WithDescription("Exchanges username / password for a Bearer token to be sent in the Authorization header.")
    .Produces<TokenResponseDto>()
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .AllowAnonymous();

// Aggregation
v1.MapGet("/aggregation", async ([AsParameters] GetAggregatedDataQuery query, ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(query, cancellationToken);
    return Results.Ok(result);
}).WithName("GetAggregatedData")
    .WithTags("Aggregation")
    .WithSummary("Retrieve aggregated data from all external APIs")
    .WithDescription(
        "Calls the external APIs (weather, news, repositories) in parallel and returns a unified, " +
        "filtered, sorted and paged list of items, together with the status of every API " +
        "(Live, Cached, Stale fallback, Unavailable or Disabled).")
    .Produces<AggregationResponseDto>()
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .RequireAuthorization();

// Statistics
v1.MapGet("/statistics", async (ISender sender, CancellationToken cancellationToken) =>
{
    var result = await sender.Send(new GetApiStatisticsQuery(), cancellationToken);
    return Results.Ok(result);
}).WithName("GetApiStatistics")
    .WithTags("Statistics")
    .WithSummary("Retrieve request statistics per external API")
    .WithDescription(
        "For each external API returns the total number of requests, failed requests and the average " +
        "response time, grouped in performance buckets (Fast / Average / Slow). Cache hits are not counted, " +
        "only real calls to the external APIs.")
    .Produces<ApiStatisticsResponseDto>()
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .RequireAuthorization();

app.Run();
