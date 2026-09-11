using System.Text.Json.Serialization;

namespace AvitoMonitoring;

internal class AvitoInfo
{
    [JsonPropertyName("id")]
    public long ItemId { get; set; }

    [JsonPropertyName("urlPath")]
    public string? ItemUrlPath { get; set; }

    [JsonPropertyName("title")]
    public string? ItemTitle { get; set; }

    [JsonPropertyName("allowTimeStamp")]
    public long ItemTimeStamp { get; set; }
    
    public string? ItemImageLink { get; set; }

    [JsonPropertyName("priceDetailed")]
    public PriceInfo? ItemPriceInformation { get; set; }
}

internal class PriceInfo
{
    [JsonInclude]
    [JsonPropertyName("value")]
    public int ItemPrice { get; set; }
}