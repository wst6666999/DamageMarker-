using System.Text.Json.Serialization;

namespace DamageMaker.Models
{
    public class BackendApiResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public List<object> Data { get; set; }

        [JsonPropertyName("output_file")]
        public string OutputFile { get; set; }

        [JsonPropertyName("output_dir")]
        public string OutputDir { get; set; }

        [JsonPropertyName("total_groups")]
        public int TotalGroups { get; set; }

        [JsonPropertyName("defect_count")]
        public int DefectCount { get; set; }

        [JsonPropertyName("processed_images")]
        public int ProcessedImages { get; set; }

        [JsonPropertyName("results_count")]
        public int ResultsCount { get; set; }
    }

    public class DamageData
    {
        [JsonPropertyName("url")]
        public string Url { get; set; }

        [JsonPropertyName("damage")]
        public float[][] DamagePoint { get; set; }
    }
}
