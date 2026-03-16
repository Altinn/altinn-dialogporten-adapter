FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS build
WORKDIR /app

COPY ["Directory.Build.props", "."]

# Main project
COPY src/Altinn.DialogportenAdapter.WebApi/*.csproj ./src/Altinn.DialogportenAdapter.WebApi/
# Dependencies
COPY src/Altinn.DialogportenAdapter.Contracts/*.csproj ./src/Altinn.DialogportenAdapter.Contracts/
# Restore project
RUN dotnet restore ./src/Altinn.DialogportenAdapter.WebApi/Altinn.DialogportenAdapter.WebApi.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet build -c Release -o out ./src/Altinn.DialogportenAdapter.WebApi/Altinn.DialogportenAdapter.WebApi.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/sdk:10.0.201@sha256:478b9038d187e5b5c29bfa8173ded5d29e864b5ad06102a12106380ee01e2e49 AS final
WORKDIR /app
EXPOSE 5011

COPY --from=build /app/out .

RUN groupadd --gid 3000 dotnet && useradd --uid 3000 --gid dotnet --no-create-home --shell /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.DialogportenAdapter.WebApi.dll" ]
