using System;
using AzureCDNImageResizer.Models;
using AzureCDNImageResizer.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.IO;
using System.Threading.Tasks;
using System.IO.Pipelines;
using System.Threading;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace AzureCDNImageResizer.Services
{
    public class ImageResizerService : IImageResizerService
    {
        private readonly IOptions<ImageResizerOptions> _settings;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;

        public ImageResizerService(
            IOptions<ImageResizerOptions> settings,
            IConfiguration configuration,
            ILogger<ImageResizerService> logger)
        {
            _settings = settings;
            _config = configuration;
            _logger = logger;
        }

        /// <summary>
        /// Resizes an image to specified size and format
        /// </summary>
        public async Task<Stream> ResizeAsync(string url, string containerKey, string size, string output, string mode,
            bool isVideo = false)
        {
            ImageSize? imageSize = isVideo ? null : ParseImageSize(size);
            return await GetResultStreamAsync(url, containerKey, imageSize, output, mode, isVideo);
        }

        /// <summary>
        /// Retrieves the blob stream and processes it if resizing is needed
        /// </summary>
        private async Task<Stream> GetResultStreamAsync(string uri, string containerKey, ImageSize? imageSize,
            string output, string mode, bool isVideo)
        {
            try
            {
                var blobStream = await GetBlobStreamAsync(containerKey, uri);

                if (ShouldSkipResizing(output, isVideo, imageSize))
                {
                    return blobStream;
                }

                return CreateResizedStream(blobStream, imageSize!.Value, output, mode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resize image from blob: {Uri} in container: {ContainerKey}", uri,
                    containerKey);
                return null;
            }
        }

        /// <summary>
        /// Retrieves a stream from Azure Blob Storage
        /// </summary>
        private async Task<Stream> GetBlobStreamAsync(string containerKey, string blobUri)
        {
            var blobServiceClient = new BlobServiceClient(_config.GetConnectionString("AzureStorage"));
            var container = blobServiceClient.GetBlobContainerClient(containerKey);
            var blobClient = container.GetBlobClient(blobUri);

            return await blobClient.OpenReadAsync(new BlobOpenReadOptions(allowModifications: false));
        }

        /// <summary>
        /// Determines if image resizing should be skipped
        /// </summary>
        private static bool ShouldSkipResizing(string output, bool isVideo, ImageSize? imageSize)
        {
            if (isVideo || imageSize is null)
            {
                return true;
            }

            if (output == "svg")
            {
                return true;
            }

            if (imageSize.Value.Name == ImageSize.OriginalImageSize)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates a resized image stream using a pipe for async processing
        /// </summary>
        private Stream CreateResizedStream(Stream sourceStream, ImageSize size, string output, string mode)
        {
            var pipe = new Pipe();

            _ = Task.Run(async () => await ProcessImageAsync(sourceStream, size, output, mode, pipe));

            return pipe.Reader.AsStream();
        }

        /// <summary>
        /// Processes the image: loads, resizes, and writes to the pipe
        /// </summary>
        private async Task ProcessImageAsync(Stream sourceStream, ImageSize size, string output, string mode, Pipe pipe)
        {
            try
            {
                using var image = await Image.LoadAsync(sourceStream);

                ApplyResize(image, size, mode);

                await WriteImageToPipeAsync(image, output, pipe);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process image stream");
                await pipe.Writer.CompleteAsync(ex);
            }
            finally
            {
                await sourceStream.DisposeAsync();
            }
        }

        /// <summary>
        /// Writes the processed image to the pipe in the specified format
        /// </summary>
        private async Task WriteImageToPipeAsync(Image image, string output, Pipe pipe)
        {
            await using var destinationStream = pipe.Writer.AsStream(true);

            WriteImage(image, destinationStream, output);
            await destinationStream.FlushAsync(CancellationToken.None);

            await pipe.Writer.FlushAsync();
            await pipe.Writer.CompleteAsync();
        }

        /// <summary>
        /// Parses image size string to ImageSize, checking predefined sizes first
        /// </summary>
        private ImageSize ParseImageSize(string value)
        {
            if (_settings.Value.PredefinedImageSizes.TryGetValue(value.ToLowerInvariant(), out var imageSize))
            {
                return imageSize;
            }

            return ImageSize.Parse(value);
        }

        /// <summary>
        /// Applies resize transformation to the image based on size and mode
        /// </summary>
        private void ApplyResize(Image image, ImageSize size, string mode)
        {
            var resizeMode = ParseResizeMode(mode);
            var resizeOptions = new ResizeOptions
            {
                Size = new Size(size.Width, size.Height),
                Mode = resizeMode
            };

            image.Mutate(x => x.Resize(resizeOptions));
        }

        /// <summary>
        /// Parses resize mode string to ResizeMode enum
        /// </summary>
        private static ResizeMode ParseResizeMode(string mode)
        {
            return (mode ?? string.Empty).ToLowerInvariant() switch
            {
                "boxpad" => ResizeMode.BoxPad,
                "pad" => ResizeMode.Pad,
                "max" => ResizeMode.Max,
                "min" => ResizeMode.Min,
                "stretch" => ResizeMode.Stretch,
                _ => ResizeMode.Crop
            };
        }

        /// <summary>
        /// Writes image to stream in the specified output format
        /// </summary>
        private static void WriteImage(Image image, Stream outputStream, string output)
        {
            switch (output?.ToLowerInvariant())
            {
                case "jpeg":
                case "jpg":
                    image.SaveAsJpeg(outputStream);
                    break;
                case "gif":
                    image.SaveAsGif(outputStream);
                    break;
                case "avif":
                case "svg":
                case "webp":
                    image.SaveAsWebp(outputStream);
                    break;
                default: // png
                    image.SaveAsPng(outputStream);
                    break;
            }
        }
    }
}