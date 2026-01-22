FROM mcr.microsoft.com/dotnet/sdk:9.0.310@sha256:0d84f05256dec37a5d1739158fd5ea197b8ad3b4e8d0e32be47b754db5963a9e AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.DialogportenAdapter.EventSimulator/*.csproj ./src/Altinn.DialogportenAdapter.EventSimulator/
RUN dotnet restore ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet build -c Release -o out ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0.12@sha256:8bbb7b9045f04a32d2eaad43b351e67f07c1f9604811dd505fc3654b4bec2176 AS base
WORKDIR /app
EXPOSE 5012

COPY --from=build /app/out .

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.DialogportenAdapter.EventSimulator.dll" ]
