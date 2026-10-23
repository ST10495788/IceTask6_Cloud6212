using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ice_Task3_NotesAPI.Services;

// All refernces are in program.cs file

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var tableConnectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");

var blobConnectionString = Environment.GetEnvironmentVariable("AzureBlobStorage");

if (string.IsNullOrWhiteSpace(tableConnectionString))
{
    throw new InvalidOperationException("AzureWebJobsStorage connection string is missing.");
}

if (string.IsNullOrWhiteSpace(blobConnectionString))
{
    throw new InvalidOperationException("AzureBlobStorage connection string is missing.");
}

builder.Services.AddSingleton(new TableServiceClient(tableConnectionString));

builder.Services.AddSingleton(new BlobServiceClient(blobConnectionString));

builder.Services.AddSingleton<UserService>();

builder.Services.AddSingleton<AuthService>();
builder.Services.AddSingleton<JwtTokenService>();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable( "APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();

//Reference List
//Docker Inc. (2026) Docker documentation. Available at: https://docs.docker.com/?utm (Accessed: 31 August 2026).
//Microsoft(2025) Install and run the Azurite emulator. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/storage/common/storage-install-azurite?tabs=visual-studio%2Cblob-storage (Accessed: 31 August 2026).
//Microsoft (2025) Run automated tests by using Azurite.Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/storage/blobs/use-azurite-to-run-automated-tests? (Accessed: 31 August 2026).
//Microsoft (2026) Azure Functions documentation. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/azure-functions/ (Accessed: 31 August 2026).
//Microsoft(2026) Azure Blob Storage documentation. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/storage/blobs/ (Accessed: 31 August 2026).
//Microsoft(2026) Azure Table Storage documentation. Microsoft Learn. Available at: https://learn.microsoft.com/en-us/azure/storage/tables/ (Accessed: 31 August 2026).
//Postman(2026) Write scripts to test API response data in Postman. Postman Learning Center. Available at: https://learning.postman.com/docs/tests-and-scripts/write-scripts/test-scripts (Accessed: 31 August 2026).
//Postman (2026) Test your API using the Collection Runner.Postman Learning Center. Available at: https://learning.postman.com/docs/tests-and-scripts/running-collections/intro-to-collection-runs (Accessed: 31 August 2026).
//Postman (2026) Export data from Postman. Postman Learning Center. Available at: https://learning.postman.com/latest-v-12/docs/getting-started/importing-and-exporting/exporting-data (Accessed: 31 August 2026).