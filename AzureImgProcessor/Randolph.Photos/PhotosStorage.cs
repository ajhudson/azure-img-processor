using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Randolph.Photos.Models;

namespace Randolph.Photos;

public static class PhotosStorage
{
    [FunctionName("PhotosStorage")]
    public static async Task<IActionResult> RunAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req, 
        [Blob(Constants.PhotosBlobPath, FileAccess.ReadWrite, Connection = Constants.StorageConnectionString)] BlobContainerClient containerClient,
        ILogger log)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonConvert.DeserializeObject<PhotoUploadModel>(body);
        var newId = Guid.NewGuid();
        var blobName = $"{newId}.jpg";

        await containerClient.CreateIfNotExistsAsync();
        var blockBlockClient = containerClient.GetBlockBlobClient(blobName);
        var photoBytes = Convert.FromBase64String(request.Photo);

        using (var ms = new MemoryStream(photoBytes))
        {
            using (var reader = new StreamReader(ms))
            {
                await blockBlockClient.UploadAsync(reader.BaseStream);
            }
        }
        
        log?.LogInformation("Successfully uploaded {BlobName}", blobName);
        
        return new OkObjectResult(newId);
    }
}