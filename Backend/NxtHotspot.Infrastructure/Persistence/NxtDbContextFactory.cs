using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NxtHotspot.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` so migrations can be created without the API's runtime configuration (JWT key etc.).
/// Generating a migration never connects to the database. Override with env NXT_DESIGN_CONNECTION to apply migrations.
/// </summary>
public sealed class NxtDbContextFactory : IDesignTimeDbContextFactory<NxtDbContext>
{
    public NxtDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("NXT_DESIGN_CONNECTION")
                 ?? "Host=localhost;Database=nxthotspot;Username=postgres";
        var options = new DbContextOptionsBuilder<NxtDbContext>().UseNpgsql(cs).Options;
        return new NxtDbContext(options);
    }
}
