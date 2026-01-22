FROM mcr.microsoft.com/dotnet/sdk:9.0.307@sha256:cdb198f8fc1dfd714c798f609582dedfe397c03318b820eecbefdea06934da14 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.DialogportenAdapter.EventSimulator/*.csproj ./src/Altinn.DialogportenAdapter.EventSimulator/
RUN dotnet restore ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet build -c Release -o out ./src/Altinn.DialogportenAdapter.EventSimulator/Altinn.DialogportenAdapter.EventSimulator.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11@sha256:f872f90df526f0282401c9be23a0b863ecd48fd452e4b413e74a5c321d2627b5 AS final
WORKDIR /app
EXPOSE 5012

COPY --from=build /app/out .

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.DialogportenAdapter.EventSimulator.dll" ]
