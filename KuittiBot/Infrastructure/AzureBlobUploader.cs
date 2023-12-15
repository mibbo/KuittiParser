using Azure.Storage.Blobs;
using KuittiBot.Functions.Domain.Abstractions;
using System;
using System.IO;
using System.Threading.Tasks;

public class AzureBlobUploader
{
    private readonly string _connectionString;
    private IFileHashCache _fileHashCache;

    // Constructor that uses the Azure Function's storage account
    public AzureBlobUploader()
    {
        _connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage", EnvironmentVariableTarget.Process)
                           ?? throw new InvalidOperationException("Azure storage account connection string not found.");
    }

    // Constructor that allows for a custom connection string
    public AzureBlobUploader(string customConnectionString)
    {
        if (string.IsNullOrWhiteSpace(customConnectionString))
            throw new ArgumentException("A valid connection string must be provided.", nameof(customConnectionString));

        _connectionString = customConnectionString;
    }

    public async Task UploadFileStreamAsync(string containerName, string fileName, Stream stream, string mimeType = null)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Check if the container exists, and create if it doesn't
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(fileName);

            // Set the ContentType if provided, else default to "application/octet-stream"
            var blobHttpHeader = new Azure.Storage.Blobs.Models.BlobHttpHeaders
            {
                ContentType = mimeType ?? "application/octet-stream"
            };

            stream.Position = 0;
            await blobClient.UploadAsync(stream, new Azure.Storage.Blobs.Models.BlobUploadOptions
            {
                HttpHeaders = blobHttpHeader
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error occurred while uploading file '{fileName}' to container '{containerName}'.", ex);
        }
    }

    /// <returns>True if file was copied and false if not.</returns>
    public async Task<bool> CopyFileToAnotherContainer(string sourceContainer, string destinationContainer, string fileName)
    {
        try
        {
            var sourceBlobClient = new BlobClient(_connectionString, sourceContainer, fileName);
            var destinationBlobClient = new BlobClient(_connectionString, destinationContainer, fileName);

            // Check if the destination blob already exists
            if (await destinationBlobClient.ExistsAsync())
            {
                Console.WriteLine($"The file '{fileName}' already exists in the destination container '{destinationContainer}'.");
                return false;
            }

            await destinationBlobClient.StartCopyFromUriAsync(sourceBlobClient.Uri);
            Console.WriteLine($"File '{fileName}' successfully copied to container '{destinationContainer}'.");
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error occurred while copying file '{fileName}' to container '{destinationContainer}'.", ex);
        }
    }
}