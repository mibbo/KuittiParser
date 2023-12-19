using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Extensions.Azure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public class AzureBlobUploader
{
    private readonly string _connectionString;
    private IUserFileInfoCache _fileHashCache;

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

    public async Task UploadFileStreamIfNotExistAsync(string containerName, string fileName, Stream stream, string mimeType = null)
    {
        try
        {
            var blobServiceClient = new BlobServiceClient(_connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            // Check if the container exists, and create if it doesn't
            await containerClient.CreateIfNotExistsAsync();

            var blobClient = containerClient.GetBlobClient(fileName);

            // Check if the destination blob already exists
            if (await blobClient.ExistsAsync())
            {
                Console.WriteLine($"The file '{fileName}' already exists in the destination container '{containerName}'.");
            }
            else
            {
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
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error occurred while uploading file '{fileName}' to container '{containerName}'.", ex);
        }
    }

    /// <returns>True if file was copied and false if not.</returns>
    public async Task<bool> CopyFileToAnotherContainerIfNotExist(string sourceContainer, string destinationContainer, string fileName)
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


    public async Task CorrectTrainingLabelJson(string container)
    {
        BlobServiceClient blobServiceClient = new BlobServiceClient(_connectionString);
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(container);

        await foreach (BlobItem blobItem in containerClient.GetBlobsAsync())
        {
            if (blobItem.Name.EndsWith("labels.json"))
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobItem.Name);

                using (var stream = new MemoryStream())
                {
                    await blobClient.DownloadToAsync(stream);
                    FormRecognizerLabelDocument document = DeserializeJsonFromStream(stream);

                    // Perform the required modifications on the document object
                    ModifyDocument(document);

                    // Serialize the modified document back to a MemoryStream
                    MemoryStream modifiedStream = SerializeToJsonStream(document);

                    // Upload the modified JSON back to the blob storage
                    modifiedStream.Position = 0; // Reset stream position to the beginning for upload
                    await blobClient.UploadAsync(modifiedStream, overwrite: true);
                }
            }
        }
    }

    private FormRecognizerLabelDocument DeserializeJsonFromStream(MemoryStream stream)
    {
        stream.Position = 0;
        using (StreamReader reader = new StreamReader(stream))
        {
            string json = reader.ReadToEnd();
            return JsonConvert.DeserializeObject<FormRecognizerLabelDocument>(json);
        }
    }

    private MemoryStream SerializeToJsonStream(FormRecognizerLabelDocument document)
    {
        string jsonString = JsonConvert.SerializeObject(document, Formatting.Indented);
        byte[] byteArray = System.Text.Encoding.UTF8.GetBytes(jsonString);
        return new MemoryStream(byteArray);
    }

    private void ModifyDocument(FormRecognizerLabelDocument document)
    {
        foreach (var label in document.Labels)
        {
            if (label.LabelName.Contains("Total")|| label.LabelName.Contains("Discount"))
            {
                if (label.Value.Count == 2)
                {
                    // Combine text
                    var combinedText = label.Value[0].Text + label.Value[1].Text;

                    combinedText = combinedText.Replace(" ", "");

                    if (combinedText.Count(c => c == ',') == 1 && !combinedText.Any(char.IsLetter))
                    {
                        // Merge bounding boxes
                        var bb1 = label.Value[0].BoundingBoxes.First().ToArray();
                        var bb2 = label.Value[1].BoundingBoxes.First().ToArray();

                        var mergedBb = new List<double> {
                                        Math.Min(bb1[0], bb2[0]), // leftmost x
                                        Math.Min(bb1[1], bb2[1]), // topmost y
                                        Math.Max(bb1[2], bb2[2]), // rightmost x
                                        Math.Min(bb1[3], bb2[3]), // top y
                                        Math.Max(bb1[4], bb2[4]), // rightmost x
                                        Math.Max(bb1[5], bb2[5]), // bottommost y
                                        Math.Min(bb1[6], bb2[6]), // leftmost x
                                        Math.Max(bb1[7], bb2[7])  // bottommost y
                    };

                        label.Value.RemoveRange(1, label.Value.Count() - 1);
                        label.Value[0].Text = combinedText;
                        label.Value[0].BoundingBoxes[0] = mergedBb;
                    }
                }

                for (var i = 0; i < label.Value.Count(); i++)
                {
                    if (!label.Value[i].Text.Any(char.IsLetter)){
                        var value = label.Value[i];
                        var text = value.Text.Replace(" ", "");
                        label.Value[i].Text = text;
                    }

                }
            }
        }
    }
}