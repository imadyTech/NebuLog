# Demo producer image.
#
# No volume and no writable state, so this one needs none of the ownership care the server image
# takes — it runs read-only with /tmp as tmpfs and writes nothing.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props NebuLog.slnx ./
COPY src/NebuLog.Contracts/NebuLog.Contracts.csproj src/NebuLog.Contracts/
COPY src/NebuLog.OpenTelemetry/NebuLog.OpenTelemetry.csproj src/NebuLog.OpenTelemetry/
COPY samples/NebuLog.Samples.DemoProducer/NebuLog.Samples.DemoProducer.csproj samples/NebuLog.Samples.DemoProducer/
RUN dotnet restore samples/NebuLog.Samples.DemoProducer/NebuLog.Samples.DemoProducer.csproj

COPY src/ src/
COPY samples/NebuLog.Samples.DemoProducer/ samples/NebuLog.Samples.DemoProducer/
RUN dotnet publish samples/NebuLog.Samples.DemoProducer/NebuLog.Samples.DemoProducer.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER app
ENTRYPOINT ["dotnet", "NebuLog.Samples.DemoProducer.dll"]
