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

            log.LogInformation("Resized and copied to stream (Length: {Length})", photoStream.Length);
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

/*
[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public static class PhotosResizer
{
    [FunctionName("PhotosResizer")]
    public static async Task RunAsync(
        [BlobTrigger(Constants.PhotosBlobPath + "/{name}", Connection = Constants.StorageConnectionString)] Stream photoStream,
        [Blob(Constants.PhotosSmallBlobPath + "/{name}", FileAccess.Write, Connection = Constants.StorageConnectionString)] Stream smallStream,
        [Blob(Constants.PhotosMediumBlobPath + "/{name}", FileAccess.Write, Connection = Constants.StorageConnectionString)] Stream mediumStream,
        ILogger log)
    {
        log.LogInformation($"Resizing....");

        try
        {
            using var msMedium = CreateMemoryStream(photoStream, ImageSize.Medium);
            await msMedium.CopyToAsync(mediumStream);

            using var msSmall = CreateMemoryStream(photoStream, ImageSize.Small);
            await msSmall.CopyToAsync(smallStream);
        }
        catch (Exception exception)
        {
            log.LogError("Error Resizing {ErrorMessage}", exception.Message);
        }
    }

    private static MemoryStream CreateMemoryStream(Stream image, ImageSize imageSize)
    {
        var ms = new MemoryStream();
        var img = Image.FromStream(image);
        var desiredWidth = imageSize == ImageSize.Medium ? img.Width / 2 : img.Width / 4;
        var ratio = (decimal)desiredWidth / img.Width;
        var resized = ResizeImage(img, desiredWidth, (int)Math.Floor(img.Height * ratio));
        resized.Save(ms, ImageFormat.Jpeg);
        ms.Position = 0;

        return ms;
    }

    private static Bitmap ResizeImage(Image image, int width, int height)
    {
        var destRect = new Rectangle(0, 0, width, height);
        var destImg = new Bitmap(width, height);
        
        destImg.SetResolution(image.HorizontalResolution, image.VerticalResolution);

        using var graphics = Graphics.FromImage(destImg);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var wrapMode = new ImageAttributes();
        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);

        return destImg;
    }

    enum ImageSize
    {
        Medium,
        Small
    }
}
*/