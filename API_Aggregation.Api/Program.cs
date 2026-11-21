using FluentValidation;
using System.ComponentModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;
using API_Aggregation.Infrastructure.Configurations;
using API_Aggregation.Infrastructure;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// Get JWT configuration
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var expiryMinutes = double.Parse(builder.Configuration["Jwt:ExpiryMinutes"] ?? "30");
var refreshExpiryMinutes = double.Parse(builder.Configuration["Jwt:RefreshExpiryMinutes"] ?? "60");
var isDevelopment = builder.Environment.IsDevelopment();

// Pre-compute JWKS response to avoid repeated base64 encoding on every request
var jwksResponse = new
{
    keys = new[]
    {
        new
        {
            kty = "oct",
            k = Convert.ToBase64String(Encoding.UTF8.GetBytes(jwtKey)),
            alg = "HS256",
            use = "sig",
            kid = "1"
        }
    }
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;    
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
    options.RequireHttpsMetadata = !isDevelopment; 

    // Allow JWT token in SignalR queries
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
builder.Services.AddSignalR();
builder.Services.AddDefaultConfiguration(builder.Configuration);
builder.Services.AddTelemetry("API_Aggregation-api", builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddApiVersioning();

var allowedOrigins = (builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5174"]).ToHashSet(StringComparer.OrdinalIgnoreCase);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins.ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();
if (isDevelopment)
{
    app.MapOpenApi();
}
app.UseCors();
app.UseCommonMiddleware();
app.UseAuthentication();
app.UseAuthorization();

// JWKS endpoint - MUST be mapped BEFORE versionedEndpointRouteBuilder to avoid versioning constraints
app.MapGet("/.well-known/jwks.json",
    () =>
    {
        return Results.Ok(jwksResponse);
    })
    .WithName("JWKS")
    .Produces(StatusCodes.Status200OK)
    .ExcludeFromDescription()
    .WithOpenApi()
    .WithDescription("Get JSON Web Key Set for JWT validation");

// Redis health check endpoint
app.MapGet("/.well-known/health/redis",
    async ([FromServices] IConnectionMultiplexer redis, ILogger<Program> logger) =>
    {
        try
        {
            var server = redis.GetServer(redis.GetEndPoints().FirstOrDefault() ?? throw new InvalidOperationException("No Redis endpoints configured"));
            await server.PingAsync();
            return Results.Ok(new { status = "healthy", service = "redis" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Redis health check failed");
            return Results.Json(new { status = "unhealthy", service = "redis", error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    })
    .WithName("RedisHealth")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable)
    .ExcludeFromDescription()
    .WithOpenApi();

var versionedEndpointRouteBuilder = app.NewVersionedApi();


app.Run();