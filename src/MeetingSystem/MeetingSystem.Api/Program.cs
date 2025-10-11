using MeetingSystem.Context;
using Polly;

using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.
builder.Services.AddDbContext<MeetingSystemDbContext>(options => options.UseSqlServer(connectionString));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddSqlServer(connectionString); // Checks if the database is reachable

var app = builder.Build();

Console.WriteLine("Applying database migrations...");
var retryPolicy = Policy
    .Handle<SqlException>()
    .WaitAndRetry(5, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), 
        (exception, timeSpan, retryCount, context) =>
        {
            Console.WriteLine(exception);
            Console.WriteLine("Retrying database connection... Attempt {RetryCount}", retryCount);
        });
retryPolicy.Execute(() =>
{        
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var dbContext = services.GetRequiredService<MeetingSystemDbContext>();
            dbContext.Database.Migrate();
            Console.WriteLine("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while applying database migrations.\n{ex}");
            throw;
        }
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();