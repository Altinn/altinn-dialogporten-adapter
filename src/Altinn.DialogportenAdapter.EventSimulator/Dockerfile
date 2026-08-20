FROM mcr.microsoft.com/dotnet/sdk:10.0.400@sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80 AS build
WORKDIR /app

COPY ["Directory.Build.props", "."]

# Main project
COPY src/Altinn.DialogportenAdapter.EventSimulator/*.csproj ./src/Altinn.DialogportenAdapter.EventSimulator/
# Dependencies
COPY src/Altinn.DialogportenAdapter.Contracts/*.csproj ./src/Altinn.DialogportenAdapter.Contracts/
# Restore project
RUN dotnet restore ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet build -c Release -o out ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11@sha256:282c2e90dd35c6a720b744f4848d3dce9de4bfb404011270cc8ee63f07e56c36 AS final
WORKDIR /app
EXPOSE 5012

COPY --from=build /app/out .

RUN groupadd --gid 3000 dotnet && useradd --uid 3000 --gid dotnet --no-create-home --shell /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.DialogportenAdapter.EventSimulator.dll" ]
