using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notsy.DBModels;
using Notsy.Helpers;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Config
var connectionString = Environment.GetEnvironmentVariable("SqlDb");
var accountName = Environment.GetEnvironmentVariable("StorageAccountName");
var containerName = Environment.GetEnvironmentVariable("ContainerName");

// EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});

// Blob storage helper
builder.Services.AddSingleton(new BlobStorageHelper(accountName!, containerName!));
builder.Services.AddSingleton<ContentTypeHelper>();
builder.Services.AddFunctionsWorkerDefaults();

builder.Build().Run();
