using System.Reflection;
using Npgsql;

namespace UruErpApp.Api;

/// <summary>
/// Runs SQL migration files embedded in the assembly in lexicographic order.
/// Applied migrations are tracked in the <c>schema_migrations</c> table so
/// each file is executed exactly once, even across rolling restarts.
/// </summary>
public static class MigrationRunner
{
    private const string TrackingTable = """
        CREATE TABLE IF NOT EXISTS schema_migrations (
            filename   TEXT        PRIMARY KEY,
            applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
        );
        """;

    public static async Task RunAsync(string connectionString, ILogger logger, CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Ensure the tracking table exists.
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = TrackingTable;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Read already-applied migration filenames.
        var applied = new HashSet<string>(StringComparer.Ordinal);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT filename FROM schema_migrations ORDER BY filename";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                applied.Add(reader.GetString(0));
        }

        // Discover embedded SQL resources, sorted so they run in numeric order.
        const string marker = ".Migrations.";
        var assembly  = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(r => r.Contains(marker) && r.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

        foreach (var resource in resources)
        {
            var filename = resource[(resource.IndexOf(marker, StringComparison.Ordinal) + marker.Length)..];
            if (applied.Contains(filename))
                continue;

            logger.LogInformation("Applying migration: {Filename}", filename);

            await using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded resource not found: {resource}");
            using var streamReader = new StreamReader(stream);
            var sql = await streamReader.ReadToEndAsync(ct);

            await using var tx = await conn.BeginTransactionAsync(ct);
            try
            {
                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText  = sql;
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction  = tx;
                    cmd.CommandText  = "INSERT INTO schema_migrations (filename) VALUES (@filename)";
                    cmd.Parameters.AddWithValue("filename", filename);
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                logger.LogInformation("Migration applied:  {Filename}", filename);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                logger.LogError(ex, "Migration failed:   {Filename}", filename);
                throw;
            }
        }
    }
}
