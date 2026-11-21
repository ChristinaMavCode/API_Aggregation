# Build from the repository root:
#   docker build -f API_Aggregation.Api/Dockerfile -t api-aggregation .
#   docker run -p 8080:8080 -e ExternalApis__OpenWeatherMap__ApiKey=<key> api-aggregation

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY API_Aggregation.sln ./
COPY API_Aggregation.Api/API_Aggregation.Api.csproj API_Aggregation.Api/
COPY API_Aggregation.Application/API_Aggregation.Application.csproj API_Aggregation.Application/
COPY API_Aggregation.Domain/API_Aggregation.Domain.csproj API_Aggregation.Domain/
COPY API_Aggregation.Infrastructure/API_Aggregation.Infrastructure.csproj API_Aggregation.Infrastructure/

RUN dotnet restore API_Aggregation.Api/API_Aggregation.Api.csproj

COPY . .

RUN dotnet publish API_Aggregation.Api/API_Aggregation.Api.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV Kestrel__Endpoints__Http__Url=http://+:8080
USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "API_Aggregation.Api.dll"]
