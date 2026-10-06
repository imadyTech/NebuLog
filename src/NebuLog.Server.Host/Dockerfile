# NebuLog server image.
#
# Written by hand rather than produced by `dotnet publish /t:PublishContainer` for one reason: the
# container runs with a read-only root filesystem and a single writable volume at /data, and Docker
# seeds a new named volume from the image's directory — including its ownership. The .NET images run
# as the non-root user `app` (UID 1654), while a volume created from nothing is owned by root, so
# without the mkdir+chown below the application cannot write its SQLite database or its Data
# Protection key ring, and the container fails to start.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, against just the project graph, so a source-only change keeps the package cache.
COPY global.json Directory.Build.props Directory.Packages.props NebuLog.slnx ./
COPY src/NebuLog.Contracts/NebuLog.Contracts.csproj src/NebuLog.Contracts/
COPY src/NebuLog.OpenTelemetry/NebuLog.OpenTelemetry.csproj src/NebuLog.OpenTelemetry/
COPY src/NebuLog.Server/NebuLog.Server.csproj src/NebuLog.Server/
COPY src/NebuLog.Server.Host/NebuLog.Server.Host.csproj src/NebuLog.Server.Host/
RUN dotnet restore src/NebuLog.Server.Host/NebuLog.Server.Host.csproj

COPY src/ src/
RUN dotnet publish src/NebuLog.Server.Host/NebuLog.Server.Host.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The writable volume. Created and chowned here so the named volume inherits the right owner.
RUN mkdir -p /data/keys && chown -R app:app /data

# `dotnet publish` on a Web SDK project already carries wwwroot — the dashboard and the browser
# sample — into /app. They are built before this image (see .github/workflows/release.yml); a build
# without them produces a working API with no UI rather than failing.
COPY --from=build /app .

USER app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0

# Uses the application itself: this image has no curl or wget for a shell probe to call.
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3     CMD ["dotnet", "NebuLog.Server.Host.dll", "--health-check"]

ENTRYPOINT ["dotnet", "NebuLog.Server.Host.dll"]
