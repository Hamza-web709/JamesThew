using JamesThew.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace JamesThew.Tests;

// Tests exercise actual SQL Server constraints/migrations, never the development database.
public class FoundationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public string DatabaseName { get; } = "JamesThew_Test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString => $@"Server=(localdb)\MSSQLLocalDB;Database={DatabaseName};Trusted_Connection=True;TrustServerCertificate=True";

    public ApplicationDbContext CreateDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlServer(ConnectionString).Options);

    public string TestUploadDir { get; } = Path.Combine(Path.GetTempPath(), $"JamesThew_Fixture_Uploads_{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Media:UploadPath", TestUploadDir);
        builder.UseSetting("Email:UseDevelopmentTestSink", "true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(ConnectionString));
        });
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
        // Build the host only after its isolated database exists.
        using var client = NewClient();
    }

    public HttpClient NewClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true
    });

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await using var db = CreateDb();
        await db.Database.EnsureDeletedAsync();
        try
        {
            if (Directory.Exists(TestUploadDir))
            {
                Directory.Delete(TestUploadDir, recursive: true);
            }
        }
        catch { }
    }
}

[CollectionDefinition("Foundation")]
public class FoundationCollection : ICollectionFixture<FoundationFixture> { }
