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

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
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
    }
}

[CollectionDefinition("Foundation")]
public class FoundationCollection : ICollectionFixture<FoundationFixture> { }
