using Azure.Storage.Blobs;
using System;
using System.IO;
using System.Threading.Tasks;

public class AzureBlobUploader
{
    private readonly string connectionString;

    // Constructor that uses the Azure Function's storage account
    public AzureBlobUploader()
    {
        connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process)
                           ?? throw new InvalidOperationException("Azure storage account connection string not found.");
    }

    // Constructor that allows for a custom connection string
    public AzureBlobUploader(string customConnectionString)
    {
        if (string.IsNullOrWhiteSpace(customConnectionString))
            throw new ArgumentException("A valid connection string must be provided.", nameof(customConnectionString));

        connectionString = customConnectionString;
    }

    public async Task UploadFileAsync(string containerName, string blobName, string filePath)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Check if the container exists, and create if it doesn't
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(blobName);

            using (var fileStream = File.OpenRead(filePath))
            {
                await blobClient.UploadAsync(fileStream, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            // Handle or log the exception as needed
            throw new InvalidOperationException("Error occurred while uploading to Azure Blob Storage.", ex);
        }
    }

    public async Task UploadFileStreamAsync(string containerName, string blobName, Stream stream, string mimeType = null)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Check if the container exists, and create if it doesn't
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(blobName);

            // Set the ContentType if provided, else default to "application/octet-stream"
            var blobHttpHeader = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = mimeType ?? "application/octet-stream"
            };

            await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = blobHttpHeader
            });
        }
        catch (Exception ex)
        {
            // Handle or log the exception as needed
            throw new InvalidOperationException("Error occurred while uploading to Azure Blob Storage.", ex);
        }
    }
}