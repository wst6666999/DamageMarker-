using System.Text.Json.Serialization;

namespace DamageMaker.Models
{

    public class DamageData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("damage")]
        public float[][] DamagePoint { get; set; }
    }
}
