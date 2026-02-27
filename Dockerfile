FROM mcr.microsoft.com/dotnet/sdk:10.0.103@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a AS build
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
FROM mcr.microsoft.com/dotnet/sdk:10.0.103@sha256:e362a8dbcd691522456da26a5198b8f3ca1d7641c95624fadc5e3e82678bd08a AS final
WORKDIR /app
EXPOSE 5011

COPY --from=build /app/out .

RUN groupadd --gid 3000 dotnet && useradd --uid 3000 --gid dotnet --no-create-home --shell /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.DialogportenAdapter.WebApi.dll" ]
