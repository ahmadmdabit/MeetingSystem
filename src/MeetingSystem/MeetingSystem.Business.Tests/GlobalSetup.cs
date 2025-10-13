using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace MeetingSystem.Business.Tests;

/// <summary>
/// This fixture manages the lifecycle of shared resources for the entire test assembly.
/// It starts a single, reusable MSSQL container once before any tests are run, and stops it after all tests are complete.
/// This provides a fast and isolated database environment for all integration-style unit tests.
/// </summary>
[SetUpFixture]
public class GlobalSetup
{
    // A flag to easily switch between a self-hosted Testcontainer and an external, pre-existing database.
    // Set to 'false' to connect to a database running from your main docker-compose.yml.
    // Set to 'true' for fully isolated, self-contained test runs (recommended for CI).
    private const bool UseSelfHostedContainer = false;

    private static IContainer? _msSqlContainer { get; set; }

    public static string ConnectionString { get; private set; } = null!;

    /// <summary>
    /// Runs once before any tests in the assembly.
    /// </summary>
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        var dbServer = "localhost";
        var dbPort = 1433;
        var dbName = "MeetingSystemTestDB";
        var dbUser = "sa";
        var dbPassword = "eefb79d9-75cc-4a28-b428-5d39f9777a95"; // eefb79d9-75cc-4a28-b428-5d39f9777a95 // yourStrong(!)Password123

        if (UseSelfHostedContainer)
        {
            // ...... Testcontainers Path: Start a dedicated, isolated container for this test run ......

            // Define and start the reusable MSSQL container.
            _msSqlContainer = new ContainerBuilder()
                .WithImage("mcr.microsoft.com/mssql/server:2017-latest")
                .WithEnvironment("ACCEPT_EULA", "Y")
                .WithEnvironment("SA_PASSWORD", dbPassword)
                .WithPortBinding(dbPort, true) // Use a random available port
                .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("/opt/mssql-tools/bin/sqlcmd", "-S", dbServer, "-U", dbUser, "-P", dbPassword, "-Q", "SELECT 1"))
                .Build();

            await _msSqlContainer.StartAsync();

            // Construct the connection string using the dynamic port and store it for all tests to use.
            ConnectionString = $"Server={dbServer},{_msSqlContainer.GetMappedPublicPort(dbPort)};Database={dbName};User Id={dbUser};Password={dbPassword};TrustServerCertificate=True;";
        }
        else
        {
            // ...... Shared Instance Path: Connect to the database from docker-compose.yml ......
            // This requires 'docker-compose up' to be running in the background.
            // The password must match the one in your .env file.
            ConnectionString = $"Server={dbServer},{dbPort};Database={dbName};User Id={dbUser};Password={dbPassword};TrustServerCertificate=True;";
        }
    }

    /// <summary>
    /// Runs once after all tests in the assembly have finished.
    /// </summary>
    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        // Only dispose of the container if we were the ones who created it.
        if (_msSqlContainer != null)
        {
            // Stop and dispose of the container.
            await _msSqlContainer.DisposeAsync();
        }
    }
}