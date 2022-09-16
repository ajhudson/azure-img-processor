using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;


namespace Randolph.Photos;

public static class PhotosResizer
{
    private static readonly Dictionary<ImageSize, (int, int)> imageDimensionsTable = new() {
        { ImageSize.ExtraSmall, (320, 200) },
        { ImageSize.Small,      (640, 400) },
        { ImageSize.Medium,     (800, 600) }
    };
    
    [FunctionName("PhotosResizer")]
    public static async Task RunAsync(
        [BlobTrigger(Constants.PhotosBlobPath + "/{name}")] Stream photoStream,
        string name,
        [Blob(Constants.PhotosMediumBlobPath + "/{name}", FileAccess.Write)] BlobContainerClient mediumBlobContainerClient, 
        [Blob(Constants.PhotosSmallBlobPath + "/{name}", FileAccess.Write)] BlobContainerClient smallBlobContainerClient,
        ILogger log)
    {
        try
        {
            await smallBlobContainerClient.CreateIfNotExistsAsync();
            await mediumBlobContainerClient.CreateIfNotExistsAsync();

            using var smallStream = new MemoryStream();
            using var mediumStream = new MemoryStream();
            
            IImageFormat format;

            using (Image<Rgba32> input = Image.Load<Rgba32>(photoStream, out format))
            {
                ResizeImage(input, smallStream, ImageSize.Small, format);
            }

            photoStream.Position = 0;
            using (Image<Rgba32> input = Image.Load<Rgba32>(photoStream, out format))
            {
                ResizeImage(input, mediumStream, ImageSize.Medium, format);
            }

            smallStream.Position = 0;
            await smallBlobContainerClient.UploadBlobAsync(name, smallStream);

            mediumStream.Position = 0;
            await mediumBlobContainerClient.UploadBlobAsync(name, mediumStream);

            log.LogInformation("Resized {Name}", name);
        }
        catch (Exception e)
        {
            log.LogError("Error running Photo Resizer: {Message}", e.Message);
        }
    }
    
    private static void ResizeImage(Image<Rgba32> input, Stream output, ImageSize size, IImageFormat format)
    {
        var dimensions = imageDimensionsTable[size];

        input.Mutate(x => x.Resize(dimensions.Item1, dimensions.Item2));
        input.Save(output, format);
    }

    private enum ImageSize { ExtraSmall, Small, Medium }
}