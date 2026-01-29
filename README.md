# Travel.WebApi

## How to Run the Service

0. **Prerequisites**
   - .NET 8.0 SDK
   - PostgreSQL database
   - RabbitMQ server
   - Docker (optional, for containerized deployment)

There are 2 ways to run service: using Docker Compose (simplest) or directly via `dotnet run`.

1. Docker Compose
   - Ensure Docker is installed and running.
   - Use command "docker-compose up -d" in terminal at the root of the project (where docker-compose.yml is located);
 
2. Dotnet run
   - Ensure PostgreSQL is running and the connection string in `appsettings.json` is correct.
   - Ensure RabbitMQ is running and the connection string in `appsettings.json` is correct.
   - From the project directory:
     ```sh
     dotnet run --project Travel.WebApi/Travel.WebApi.csproj
     ```

4. **API Usage**
   - Swagger UI: 
       - http://localhost:8080 - for docker
       - https://localhost:7113 - for local run
   - Health: `GET /health`
   - Metrics: `GET /metrics` (Prometheus format)

5. The `Items` table is pre-populated with 10 elements (id = 1...10) standard records with quantity = 100 for testing purposes.

## Design Decisions & Trade-offs

1. Project is divided into folders instead of separate projects for simplicity.
2. There is only one service. This is required by design. However, in a real-world scenario, it would be better to separate Order and Inventory services.
3. Use transaction to ensure data consistency between Orders and Inventory. This transaction can be a bottleneck under high load. Have to consider saga pattern
4. Data model is simplified for demonstration purposes. In a production system, more fields and relationships would be necessary or modified (ex: one-to-one between Items and Inventory)
5. EF migrations are applied from code on startup for simplicity. In production, consider using a dedicated migration tool or process.
6. Minimal validation and error handling is implemented for brevity. In production, more robust validation, logging, and error handling would be necessary.
7. For simplicity, the service does not implement authentication or authorization. In a real-world scenario, secure the endpoints appropriately.
8. Metrics are exposed via a simple endpoint. In production, consider integrating with a more comprehensive monitoring solution.
9. Used EF Core migrations to seed initial data. Trade-off: not as flexible as runtime seeding for complex scenarios
10. Credentials were hardcoded in configuration files for simplicity. they should be stored in some key storage and not published in git.
