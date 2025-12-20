using System;
using System.Linq;
using AzureCDNImageResizer.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using AzureCDNImageResizer.Services;
using AzureCDNImageResizer.Options;
using Microsoft.Net.Http.Headers;

namespace AzureCDNImageResizer.Functions
{
    public class ImageResizeFunction
    {
        private readonly IImageResizerService _imageResizerService;
        private readonly IOptions<ClientCacheOptions> _clientCacheOptions;

        private static readonly IReadOnlyList<string> VideoOutputs = new List<string>
        {
            "mp4", "mov", "avi", "wmv", "webm"
        };

        private static readonly IReadOnlyList<string> ImageOutputs = new List<string>
        {
            "jpeg", "jpg", "gif", "png", "webp", "avif", "svg"
        };

        public ImageResizeFunction(IImageResizerService imageProxyService,
            IOptions<ClientCacheOptions> clientCacheOptions)
        {
            _imageResizerService = imageProxyService;
            _clientCacheOptions = clientCacheOptions;
        }

        [FunctionName("ResizeImage")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "ResizeImage/{*restOfPath}")]
            HttpRequest req,
            string restOfPath)
        {
            if (IsNotModified(req))
            {
                return new StatusCodeResult((int)HttpStatusCode.NotModified);
            }

            try
            {
                var (container, url) = ParsePath(restOfPath);

                if (string.IsNullOrEmpty(container))
                {
                    return new OkResult();
                }

                if (string.IsNullOrEmpty(url))
                {
                    return new BadRequestObjectResult("URL is required");
                }

                var queryParams = QueryParameters.FromQuery(req.Query, url);
                var output = ValidateAndNormalizeOutput(queryParams.Output, url);
                var size = DetermineSize(queryParams.Size, queryParams.Width, queryParams.Height);
                var isVideo = IsVideoOutput(output);

                var imageStream = await _imageResizerService.ResizeAsync(
                    url, container, size, output, queryParams.Mode, isVideo);

                if (imageStream == null)
                {
                    return new NotFoundResult();
                }

                var mimeType = GetMimeType(output, isVideo);
                SetCacheHeaders(req.HttpContext.Response.GetTypedHeaders());

                return new FileStreamResult(imageStream, mimeType);
            }
            catch (Exception)
            {
                return new BadRequestResult();
            }
        }

        /// <summary>
        /// Checks if the request has If-Modified-Since header indicating cached version
        /// </summary>
        private static bool IsNotModified(HttpRequest request)
        {
            return request.HttpContext.Request.GetTypedHeaders().IfModifiedSince.HasValue;
        }

        /// <summary>
        /// Parses the path to extract container name and image URL
        /// </summary>
        private static (string container, string url) ParsePath(string restOfPath)
        {
            var urlSplit = restOfPath.Split("/", StringSplitOptions.RemoveEmptyEntries);
            
            if (urlSplit.Length == 0)
            {
                return (string.Empty, string.Empty);
            }

            var container = urlSplit[0];
            var url = "/" + string.Join("/", urlSplit.Skip(1));

            return (container, url.TrimEnd('/'));
        }

        /// <summary>
        /// Validates and normalizes the output format
        /// </summary>
        private static string ValidateAndNormalizeOutput(string output, string url)
        {
            var validOutputs = new List<string>(ImageOutputs);
            validOutputs.AddRange(VideoOutputs);

            if (!validOutputs.Contains(output))
            {
                return url.ToSuffix();
            }

            return output;
        }

        /// <summary>
        /// Determines the size string from query parameters
        /// </summary>
        private static string DetermineSize(string size, int width, int height)
        {
            if (!string.IsNullOrEmpty(size))
            {
                return size;
            }

            return $"{width}x{height}";
        }

        /// <summary>
        /// Determines if the output format is a video
        /// </summary>
        private static bool IsVideoOutput(string output)
        {
            return VideoOutputs.Contains(output);
        }

        /// <summary>
        /// Gets the MIME type for the given output format
        /// </summary>
        private static string GetMimeType(string output, bool isVideo)
        {
            return output.ToLowerInvariant() switch
            {
                "jpeg" or "jpg" => "image/jpeg",
                "gif" => "image/gif",
                "png" => "image/png",
                "webp" => "image/webp",
                "avif" => "image/webp",
                "svg" => "image/svg",
                "mp4" => "video/mp4",
                "mov" => "video/mov",
                "avi" => "video/avi",
                "wmv" => "video/wmv",
                "webm" => "video/webm",
                _ => isVideo ? "video/mp4" : "image/jpeg"
            };
        }

        /// <summary>
        /// Sets cache headers on the response
        /// </summary>
        private void SetCacheHeaders(ResponseHeaders responseHeaders)
        {
            responseHeaders.CacheControl = new CacheControlHeaderValue { Public = true };
            responseHeaders.LastModified = new DateTimeOffset(new DateTime(1900, 1, 1));
            responseHeaders.Expires = new DateTimeOffset(
                (DateTime.Now + _clientCacheOptions.Value.MaxAge).ToUniversalTime());
        }
    }
}

