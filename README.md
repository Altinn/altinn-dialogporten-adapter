# altinn-dialogporten-adapter
Altinn App Storage adapter that handles synchronization of instances to dialogs

# How to run the docker-image locally

Prerequisites
- Dialogporten API runs on **http** port 5123
- You have the required usersecrets for the application

```bash
# Build the image
podman build -t dialogporten-adapter .

# Run the image
# Find the usersecret id in your src/Altinn.DialogportenAdapter.WebApi/Altinn.DialogportenAdapter.WebApi.csproj
podman run -p 8080:8080 -e ASPNETCORE_ENVIRONMENT=DevelopmentDocker -v ~/.microsoft/usersecrets/<replace-me-with-usersecret-id>/secrets.json:/altinn-appsettings/altinn-dbsettings-secret.json dialogporten-adapter

# Make a request to the docker instance with Rider scratch file
Run the SyncByInstanceId.http scratch file. Remember to select "Run with: docker". 
```

> Note: We use a separate configuration for the docker-image. See: appsettings.DevelopmentDocker.json 

# How to run locally with ServiceBusEmulator

1. Set this in your user-secrets:
```json
{
  ...
  "WolverineSettings:ServiceBusConnectionString": "Endpoint=sb://localhost:5672;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
  "WolverineSettings:ManagementConnectionString": "Endpoint=sb://localhost:5300;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;" 
}
```
2. Start service-bus-emulator 

```bash
docker-compose -f docker-compose-service-bus.yml up
``` 

#### Note on Colima on arm-Mac
If you use Colima on an Arm Mac, the msql image may crash. You can fix it by starting the vm with rosetta enabled:
```bash
colima start --cpu 4 --memory 4 --disk 100 --vm-type=vz --vz-rosetta
```

#### Note on Podman on arm-Mac
If you use Podman on an Arm Mac, the mssql image may crash. You can fix it by starting the vm with rosetta enabled:
# https://blog.podman.io/2025/08/podman-5-6-released-rosetta-status-update/
