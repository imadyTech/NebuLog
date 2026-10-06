# NebuShop payments service: the downstream half of the cross-service trace scenario.
#
# No volume and no writable state; runs read-only with /tmp as tmpfs. Reached only from the orders
# service on the internal network; it publishes no port.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props NebuLog.slnx ./
COPY src/NebuLog.Contracts/NebuLog.Contracts.csproj src/NebuLog.Contracts/
COPY src/NebuLog.OpenTelemetry/NebuLog.OpenTelemetry.csproj src/NebuLog.OpenTelemetry/
COPY samples/NebuShop.Payments/NebuShop.Payments.csproj samples/NebuShop.Payments/
RUN dotnet restore samples/NebuShop.Payments/NebuShop.Payments.csproj

COPY src/ src/
COPY samples/NebuShop.Payments/ samples/NebuShop.Payments/
RUN dotnet publish samples/NebuShop.Payments/NebuShop.Payments.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app .

USER app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0

ENTRYPOINT ["dotnet", "NebuShop.Payments.dll"]
