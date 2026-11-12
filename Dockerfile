# ---- Build stage: full SDK, only used to compile ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first in its own layer so package downloads are cached when only code changes.
COPY src/StockTrack.Api/StockTrack.Api.csproj src/StockTrack.Api/
RUN dotnet restore src/StockTrack.Api/StockTrack.Api.csproj

COPY src/ src/
RUN dotnet publish src/StockTrack.Api/StockTrack.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime stage: small ASP.NET image, no SDK or source code ----
FROM mcr.microsoft.com/dotnet/aspnet:11.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
EXPOSE 8080

# Run as the image's built-in non-root user.
USER $APP_UID

ENTRYPOINT ["dotnet", "StockTrack.Api.dll"]
