using System.Text.Json.Serialization;

namespace SchoolProject.Models.Search
{
    public class SchoolDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("doc_type")]
        public string? DocType { get; set; }

        [JsonPropertyName("entity_id")]
        public int EntityId { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("locality_id")]
        public int? LocalityId { get; set; }

        [JsonPropertyName("locality_name")]
        public string? LocalityName { get; set; }

        [JsonPropertyName("city_id")]
        public int? CityId { get; set; }

        [JsonPropertyName("city_name")]
        public string? CityName { get; set; }

        [JsonPropertyName("nsewc_id")]
        public int? NsewcId { get; set; }

        [JsonPropertyName("coed_id")]
        public int? CoedId { get; set; }

        [JsonPropertyName("ownership_id")]
        public int? OwnershipId { get; set; }

        [JsonPropertyName("is_active")]
        public bool IsActive { get; set; }

        [JsonPropertyName("is_featured")]
        public bool IsFeatured { get; set; }

        [JsonPropertyName("listing_rank")]
        public int ListingRank { get; set; }

        [JsonPropertyName("keywords")]
        public string? Keywords { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}