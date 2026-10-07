using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;

namespace Birmarket.Tests;

public sealed class DemoApplicationFactory : WebApplicationFactory<Birmarket.Api.Program>
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), "birmarket-tests", Guid.NewGuid().ToString("N"), "store.sqlite3");
    private readonly string? previousDemo = Environment.GetEnvironmentVariable("birmarket_demo_mode");
    private readonly string? previousPath = Environment.GetEnvironmentVariable("birmarket_demo_sqlite_path");
    private readonly string? previousProtectionKey = Environment.GetEnvironmentVariable("birmarket_data_protection_key");

    public DemoApplicationFactory()
    {
        Environment.SetEnvironmentVariable("birmarket_demo_mode", "true");
        Environment.SetEnvironmentVariable("birmarket_demo_sqlite_path", databasePath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment(Environments.Development);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        Environment.SetEnvironmentVariable("birmarket_demo_mode", previousDemo);
        Environment.SetEnvironmentVariable("birmarket_demo_sqlite_path", previousPath);
        Environment.SetEnvironmentVariable("birmarket_data_protection_key", previousProtectionKey);
        var directory = Path.GetDirectoryName(databasePath);
        if (directory is not null && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}
