using AzureCDNImageResizer.Models;
using System.Collections.Generic;

namespace AzureCDNImageResizer.Options
{
    public class ImageResizerOptions
    {
        public const string CertificateSm = "200x200";
        public const string DefaultSize = "400x400";

        public readonly IDictionary<string, ImageSize> PredefinedImageSizes = new Dictionary<string, ImageSize>
        {
            { "c_sm", ImageSize.Parse(CertificateSm, "c_sm") },
            { "d", ImageSize.Parse(DefaultSize, "d") },
        };
    }
}