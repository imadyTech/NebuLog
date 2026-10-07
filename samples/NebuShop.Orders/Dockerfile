# NebuShop orders service: the demo shop's front end and its two equivalent API implementations.
#
# No volume and no writable state; runs read-only with /tmp as tmpfs. The container publishes no
# port of its own — the NebuLog host reverse-proxies it under /apps/shop.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props NebuLog.slnx ./
COPY src/NebuLog.Contracts/NebuLog.Contracts.csproj src/NebuLog.Contracts/
COPY src/NebuLog.OpenTelemetry/NebuLog.OpenTelemetry.csproj src/NebuLog.OpenTelemetry/
COPY samples/NebuShop.Orders/NebuShop.Orders.csproj samples/NebuShop.Orders/
RUN dotnet restore samples/NebuShop.Orders/NebuShop.Orders.csproj

COPY src/ src/
COPY samples/NebuShop.Orders/ samples/NebuShop.Orders/
RUN dotnet publish samples/NebuShop.Orders/NebuShop.Orders.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# The shop page is built before this image (see .github/workflows/release.yml) and travels in
# wwwroot; a build without it yields a working API and a placeholder page rather than a failure.
COPY --from=build /app .

USER app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0

ENTRYPOINT ["dotnet", "NebuShop.Orders.dll"]
