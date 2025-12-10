FROM mcr.microsoft.com/dotnet/sdk:9.0.102-alpine3.20@sha256:a9d99ca1f16b123abd916a9e95b4304c3d3e03e86781e24ca565119badd5cad6 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.DialogportenAdapter.EventSimulator/*.csproj ./src/Altinn.DialogportenAdapter.EventSimulator/
RUN dotnet restore ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet build -c Release -o out ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0.1-alpine3.20@sha256:5b2b4d6ef37864167055e4e215eb10bd77217148068f7c055c900ca22d14c945 AS final
WORKDIR /app
EXPOSE 5012

COPY --from=build /app/out .

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.DialogportenAdapter.EventSimulator.dll" ]
