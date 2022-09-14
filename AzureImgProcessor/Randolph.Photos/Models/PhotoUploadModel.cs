using Newtonsoft.Json;

namespace Randolph.Photos.Models;

public class PhotoUploadModel
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }

    [JsonProperty("tags")]
    public string[] Tags { get; set; }

    [JsonProperty("photo")]
    public string Photo { get; set; }
}