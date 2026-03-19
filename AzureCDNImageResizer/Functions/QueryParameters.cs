using AzureCDNImageResizer.Extensions;
using Microsoft.AspNetCore.Http;

namespace AzureCDNImageResizer.Functions;

/// <summary>
/// Helper record to hold query parameters
/// </summary>
public record QueryParameters(
    string Size,
    int Width,
    int Height,
    string Output,
    string Mode,
    bool HasResizeRequest,
    bool HasOutputRequest,
    bool HasModeRequest)
{
    public const string SizeKey = "s";
    public const string WidthKey = "w";
    public const string HeightKey = "h";
    public const string OutputKey = "o";
    public const string ModeKey = "m";
    
    /// <summary>
    /// Extracts query parameters from the request
    /// </summary>
    public static QueryParameters FromQuery(IQueryCollection query, string url)
    {
        var hasSizeRequest = query.ContainsKey(SizeKey);
        var hasWidthRequest = query.ContainsKey(WidthKey);
        var hasHeightRequest = query.ContainsKey(HeightKey);
        var hasOutputRequest = query.ContainsKey(OutputKey);
        var hasModeRequest = query.ContainsKey(ModeKey);

        var size = hasSizeRequest ? query[SizeKey].ToString() : string.Empty;
        var width = hasWidthRequest ? query[WidthKey].ToString().ToInt() : 0;
        var height = hasHeightRequest ? query[HeightKey].ToString().ToInt() : 0;
        var output = hasOutputRequest
            ? query[OutputKey].ToString().Replace(".", "")
            : url.ToSuffix();
            
        var mode = hasModeRequest ? query[ModeKey].ToString() : string.Empty;

        return new QueryParameters(
            size,
            width,
            height,
            output,
            mode,
            hasSizeRequest || hasWidthRequest || hasHeightRequest,
            hasOutputRequest,
            hasModeRequest);
    }
}