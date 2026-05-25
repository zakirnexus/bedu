using System.Collections.Generic;

namespace SchoolProject.Models.Search
{
    public class ElasticSearchDocument
    {
        public string Id { get; set; } = "";
        public string DocType { get; set; } = "";
        public int EntityId { get; set; }
        public string? Title { get; set; }
        public string? Slug { get; set; }
        public string? Url { get; set; }

        public int? CityId { get; set; }
        public string? CityName { get; set; }
        public string? CitySlug { get; set; }

        public int? LocalityId { get; set; }
        public string? LocalityName { get; set; }
        public string? LocalitySlug { get; set; }

        public int? NsewcId { get; set; }
        public string? NsewcName { get; set; }

        public List<int> SyllabusIds { get; set; } = new();
        public List<string> SyllabusNames { get; set; } = new();
        public List<string> SyllabusSlugs { get; set; } = new();

        public int? CoedId { get; set; }
        public string? CoedName { get; set; }

        public int? OwnershipId { get; set; }
        public string? OwnershipName { get; set; }

        public int? StateId { get; set; }
        public string? StateName { get; set; }

        public List<int> CourseIds { get; set; } = new();
        public List<string> CourseNames { get; set; } = new();
        public List<string> SpecializationNames { get; set; } = new();

        public string? Address { get; set; }
        public string? Pincode { get; set; }
        public string? Keywords { get; set; }
        public string? MetaDescription { get; set; }
        public string? Description { get; set; }

        public int? ListingRank { get; set; }
        public bool IsSponsored { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }

        public IEnumerable<string> Suggest { get; set; } = new List<string>();
    }
}
