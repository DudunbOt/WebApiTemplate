# Health Checks

Your backend template includes comprehensive health check endpoints for monitoring application health and dependencies.

## Available Endpoints

### 1. `/health` - Full Health Check

Returns detailed health information about all dependencies.

**Usage:**
```http
GET /health
```

**Response (Healthy):**
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "sqlserver",
      "status": "Healthy",
      "description": null,
      "duration": 45.2,
      "exception": null,
      "data": {}
    },
    {
      "name": "redis",
      "status": "Healthy",
      "description": null,
      "duration": 12.3,
      "exception": null,
      "data": {}
    }
  ],
  "totalDuration": 57.5
}
```

**Response (Unhealthy):**
```json
{
  "status": "Unhealthy",
  "checks": [
    {
      "name": "sqlserver",
      "status": "Healthy",
      "description": null,
      "duration": 42.1,
      "exception": null,
      "data": {}
    },
    {
      "name": "redis",
      "status": "Unhealthy",
      "description": null,
      "duration": 5002.8,
      "exception": "It was not possible to connect to the redis server(s).",
      "data": {}
    }
  ],
  "totalDuration": 5044.9
}
```

**Status Codes:**
- `200 OK` - All checks passed
- `503 Service Unavailable` - One or more checks failed

### 2. `/health/live` - Liveness Probe

Simple endpoint that returns `200 OK` if the application is running. Does not check dependencies.

**Usage:**
```http
GET /health/live
```

**Response:**
```
Healthy
```

**Purpose:**
- Used by orchestrators (Kubernetes, Docker Swarm) to determine if the container should be restarted
- Returns 200 if the process is alive, regardless of dependency health
- Fast response (no external checks)

**Status Codes:**
- `200 OK` - Application process is running

### 3. `/health/ready` - Readiness Probe

Checks if the application and its dependencies are ready to serve traffic.

**Usage:**
```http
GET /health/ready
```

**Response:**
```
Healthy
```

**Purpose:**
- Used by load balancers and orchestrators to determine if traffic should be routed to this instance
- Checks database and cache connectivity
- Returns 503 if any critical dependency is unavailable

**Status Codes:**
- `200 OK` - Application is ready to receive traffic
- `503 Service Unavailable` - Dependencies are not healthy

## Monitored Dependencies

### SQL Server Database
- **Check Name:** `sqlserver`
- **Tags:** `db`, `sql`, `sqlserver`
- **What it checks:** Connects to SQL Server and executes a simple query
- **Configuration:** Uses connection string from `appsettings.json` → `ConnectionStrings:default`

### Redis Cache
- **Check Name:** `redis`
- **Tags:** `cache`, `redis`
- **What it checks:** Connects to Redis and performs a PING
- **Configuration:** Uses connection string from `appsettings.json` → `ConnectionStrings:redis`

## Kubernetes/Docker Integration

### Example Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: webapi-template
spec:
  replicas: 3
  selector:
    matchLabels:
      app: webapi-template
  template:
    metadata:
      labels:
        app: webapi-template
    spec:
      containers:
      - name: webapi
        image: your-registry/webapi-template:latest
        ports:
        - containerPort: 8080
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 3
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 2
```

### Docker Compose

```yaml
version: '3.8'

services:
  webapi:
    image: your-registry/webapi-template:latest
    ports:
      - "8080:8080"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
```

## Monitoring Integration

### Prometheus

Add prometheus health check metrics:

```bash
dotnet add package AspNetCore.HealthChecks.Prometheus.Metrics
```

Update Program.cs:
```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(...)
    .AddRedis(...)
    .ForwardToPrometheus();

// Expose metrics
app.MapMetrics();
```

### Azure Application Insights

```csharp
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.AddHealthChecks()
    .AddApplicationInsightsPublisher();
```

### Custom Monitoring Tools

Use the `/health` endpoint with any monitoring tool:
- Datadog
- New Relic
- Grafana
- Uptime Robot
- Pingdom

Example with curl:
```bash
# Check overall health
curl http://localhost:5000/health

# Exit code will be 0 if healthy, non-zero if unhealthy
curl -f http://localhost:5000/health/ready || echo "Service not ready!"
```

## Adding Custom Health Checks

### 1. Create Custom Health Check

```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;

public class ExternalApiHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;

    public ExternalApiHealthCheck(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("https://api.example.com/health", cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy("External API is responding");
            }

            return HealthCheckResult.Degraded($"External API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("External API is unavailable", ex);
        }
    }
}
```

### 2. Register Custom Health Check

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(...)
    .AddRedis(...)
    .AddCheck<ExternalApiHealthCheck>(
        "external-api",
        tags: new[] { "external", "api" });
```

## Health Check Status Levels

### Healthy
- All dependencies are functioning correctly
- Application is ready to serve traffic
- HTTP 200 OK

### Degraded
- Application is functional but with reduced capacity
- Some non-critical dependencies may be unavailable
- HTTP 200 OK (still accepts traffic)

### Unhealthy
- One or more critical dependencies are unavailable
- Application cannot function properly
- HTTP 503 Service Unavailable

## Troubleshooting

### Health Check Always Returns Unhealthy

**Problem:** `/health` endpoint always returns 503

**Solutions:**
1. Check database connection string in appsettings.json
2. Verify SQL Server is running and accessible
3. Check Redis connection string
4. Verify Redis is running and accessible
5. Check firewall rules
6. Review logs for connection errors

```bash
# Check SQL Server connectivity
sqlcmd -S localhost -U sa -P YourPassword -Q "SELECT 1"

# Check Redis connectivity
redis-cli ping
```

### Slow Health Check Responses

**Problem:** Health checks take too long to respond

**Solutions:**
1. Add timeout configurations:

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: builder.Configuration.GetConnectionString("default")!,
        healthQuery: "SELECT 1",
        name: "sqlserver",
        timeout: TimeSpan.FromSeconds(3))
    .AddRedis(
        redisConnectionString: builder.Configuration.GetConnectionString("redis")!,
        name: "redis",
        timeout: TimeSpan.FromSeconds(3));
```

2. Use separate endpoints for expensive checks
3. Cache health check results

### Container Keeps Restarting

**Problem:** Kubernetes keeps restarting the pod

**Possible Causes:**
1. `initialDelaySeconds` too short - app not ready yet
2. Database migrations running on startup
3. Dependencies not ready

**Solution:** Increase `initialDelaySeconds` and add `startupProbe`:

```yaml
startupProbe:
  httpGet:
    path: /health/live
    port: 8080
  initialDelaySeconds: 30
  periodSeconds: 10
  failureThreshold: 30  # Allow up to 5 minutes for startup
```

## Best Practices

### 1. Separate Liveness and Readiness
- **Liveness** (`/health/live`) - Should only check if the process is alive
- **Readiness** (`/health/ready`) - Should check if dependencies are healthy

### 2. Set Appropriate Timeouts
- Health checks should respond within 1-3 seconds
- Set shorter timeouts for critical checks
- Longer timeouts for less critical dependencies

### 3. Tag Your Checks
```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(..., tags: new[] { "db", "critical" })
    .AddRedis(..., tags: new[] { "cache", "optional" });
```

Then filter by tags:
```csharp
app.MapHealthChecks("/health/critical", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("critical")
});
```

### 4. Include Health Checks in CI/CD
```bash
# In your deployment script
echo "Waiting for service to be ready..."
for i in {1..30}; do
    if curl -f http://your-service/health/ready; then
        echo "Service is ready!"
        exit 0
    fi
    echo "Waiting..."
    sleep 10
done
echo "Service failed to become ready"
exit 1
```

### 5. Monitor Health Check Metrics
- Track health check duration trends
- Alert on degraded health before failures
- Monitor health check endpoint response times

## Summary

✅ **3 Endpoints** - `/health`, `/health/live`, `/health/ready`
✅ **SQL Server Check** - Validates database connectivity
✅ **Redis Check** - Validates cache connectivity
✅ **Kubernetes Ready** - Liveness and readiness probes configured
✅ **Detailed JSON Response** - Status, duration, and error details
✅ **Extensible** - Easy to add custom health checks
✅ **Production Ready** - Follows cloud-native best practices

Your health checks are ready for production deployment with Kubernetes, Docker, or any cloud platform!
