using AzureCDNImageResizer.Extensions;
using AzureCDNImageResizer.Options;
using Microsoft.AspNetCore.Http;

namespace AzureCDNImageResizer.Functions;

/// <summary>
/// Helper record to hold query parameters
/// </summary>
public record QueryParameters(string Size, int Width, int Height, string Output, string Mode)
{
    public const string SizeKey = "s";
    public const string WidthKey = "w";
    public const string HeightKey = "h";
    public const string OutputKey = "o";
    public const string ModeKey = "m";

    public const string DefaultSize = "d";
    
    /// <summary>
    /// Extracts query parameters from the request
    /// </summary>
    public static QueryParameters FromQuery(IQueryCollection query, string url)
    {
        var size = query.ContainsKey(SizeKey) ? query[SizeKey].ToString() : DefaultSize;
        var width = query.ContainsKey(WidthKey) ? query[WidthKey].ToString().ToInt() : 0;
        var height = query.ContainsKey(HeightKey) ? query[HeightKey].ToString().ToInt() : 0;
        var output = query.ContainsKey(OutputKey)
            ? query[OutputKey].ToString().Replace(".", "")
            : url.ToSuffix();
            
        var mode = query.ContainsKey(ModeKey) ? query[ModeKey].ToString() : string.Empty;

        return new QueryParameters(size, width, height, output, mode);
    }
}