# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /backend

COPY API_Aggregation/*.sln ./

COPY API_Aggregation/API_Aggregation.Api/API_Aggregation.Api.csproj API_Aggregation/API_Aggregation.Api/
COPY API_Aggregation/API_Aggregation.Application/API_Aggregation.Application.csproj API_Aggregation/API_Aggregation.Application/
COPY API_Aggregation/API_Aggregation.Domain/API_Aggregation.Domain.csproj API_Aggregation/API_Aggregation.Domain/
COPY API_Aggregation/API_Aggregation.Infrastructure/API_Aggregation.Infrastructure.csproj API_Aggregation/API_Aggregation.Infrastructure/
COPY API_Aggregation/API_Aggregation.UnitTests/API_Aggregation.UnitTests.csproj API_Aggregation/API_Aggregation.UnitTests/

RUN dotnet restore API_Aggregation/API_Aggregation.Api/API_Aggregation.Api.csproj

COPY . .

WORKDIR /backend/API_Aggregation/API_Aggregation.Api

RUN dotnet publish -c Debug -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER app
EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "API_Aggregation.Api.dll"]