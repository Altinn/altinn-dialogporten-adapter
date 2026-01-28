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