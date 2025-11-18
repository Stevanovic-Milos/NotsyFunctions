using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notsy.DBModels;
using Notsy.Helpers;
using Microsoft.Data.SqlClient;
using Notsy;

var builder = FunctionsApplication.CreateBuilder(args);

SqlAuthenticationProvider.SetProvider(
    SqlAuthenticationMethod.ActiveDirectoryManagedIdentity,
    new AzureSQLAuthProvider());

builder.ConfigureFunctionsWebApplication();

// Config
var connectionString = Environment.GetEnvironmentVariable("SqlDb");
var accountName = Environment.GetEnvironmentVariable("StorageAccountName");
var containerName = Environment.GetEnvironmentVariable("ContainerName");
var clientId = Environment.GetEnvironmentVariable("ClientId");


// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var sqlConnection = new SqlConnection(connectionString);
    options.UseSqlServer(sqlConnection);
});

// Blob storage helper
builder.Services.AddSingleton(new BlobStorageHelper(accountName!, containerName!, clientId!));
builder.Services.AddSingleton<ContentTypeHelper>();
builder.Services.AddFunctionsWorkerDefaults();

builder.Build().Run();