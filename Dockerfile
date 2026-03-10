# Stage 1 — Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and all project files first (layer cache for restore)
COPY OnTimeCRM.sln ./
COPY src/OnTimeCRM.Domain/OnTimeCRM.Domain.csproj                     src/OnTimeCRM.Domain/
COPY src/OnTimeCRM.Application/OnTimeCRM.Application.csproj           src/OnTimeCRM.Application/
COPY src/OnTimeCRM.Infrastructure/OnTimeCRM.Infrastructure.csproj     src/OnTimeCRM.Infrastructure/
COPY src/OnTimeCRM.API/OnTimeCRM.API.csproj                           src/OnTimeCRM.API/

RUN dotnet restore src/OnTimeCRM.API/OnTimeCRM.API.csproj

# Copy rest of source and publish
COPY . .
RUN dotnet publish src/OnTimeCRM.API/OnTimeCRM.API.csproj \
    -c Release \
    -o /app/publish

# Stage 2 — Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "OnTimeCRM.API.dll"]
