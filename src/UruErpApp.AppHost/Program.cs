using Aspire.Hosting.ApplicationModel;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var uruerp = postgres.AddDatabase("uruerp");

// ── PgBouncer ────────────────────────────────────────────────────────────────
// Built from the local pgbouncer/ Dockerfile — mirrors the production sidecar.
// Containers share the Aspire Docker network, so postgres is reachable at
// the container-internal host/port resolved via EndpointProperty.Host/Port.
var pgEndpoint = postgres.GetEndpoint("tcp");

var pgbouncer = builder.AddDockerfile("pgbouncer", "../../pgbouncer")
    .WithEnvironment("DB_USER", "postgres")
    .WithEnvironment("DB_NAME", "uruerp")
    .WithEnvironment("DB_PASSWORD", postgres.Resource.PasswordParameter!)
    .WithEnvironment("SERVER_TLS_SSLMODE", "disable")   // no TLS in local Docker
    .WithEnvironment("POOL_MODE", "transaction")
    .WithEnvironment("MAX_CLIENT_CONN", "100")
    .WithEnvironment("DEFAULT_POOL_SIZE", "10")
    .WithEnvironment("DB_HOST", pgEndpoint.Property(EndpointProperty.Host))
    .WithEnvironment("DB_PORT", pgEndpoint.Property(EndpointProperty.Port))
    .WithEndpoint(targetPort: 5432, name: "tcp")
    .WaitFor(postgres);

// ── API ───────────────────────────────────────────────────────────────────────
// Inject a connection string pointing to PgBouncer so the API never talks
// directly to postgres — identical to the production topology.
var pgbEndpoint = pgbouncer.GetEndpoint("tcp");

var api = builder.AddProject<Projects.UruErpApp_Api>("api")
    .WithEnvironment("ConnectionStrings__uruerp",
        ReferenceExpression.Create(
            $"Host={pgbEndpoint.Property(EndpointProperty.Host)};Port={pgbEndpoint.Property(EndpointProperty.Port)};Database=uruerp;Username=postgres;Password={postgres.Resource.PasswordParameter!}"))
    .WaitFor(pgbouncer);

builder.AddNpmApp("web", "../uerp-web", "dev")
    .WithHttpEndpoint(env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(api);

builder.Build().Run();
