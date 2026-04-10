# Adapter Integration Tests

## Nice to know

- Locally we run Azure Service Bus and MsSql as reusable testcontainers to speed up development experience.
  This means the tests will leave these two containers running.

  To stop:
    ```bash
     docker container stop dialogporten-adapter-it-asb
     docker container stop dialogporten-adapter-it-msql
    ```

  To delete:
    ```bash
     docker container rm dialogporten-adapter-it-asb
     docker container rm dialogporten-adapter-it-msql
     docker network rm dialogporten-adapter-it-network
    ```
