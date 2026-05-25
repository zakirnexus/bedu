namespace SchoolProject.Models.Search
{
    public class ElasticSearchRequest
    {
        public string? Query { get; set; }
        public string? DocType { get; set; }
        public string? CitySlug { get; set; }
        public string? LocalitySlug { get; set; }
        public string? NsewcName { get; set; }
        public string? SyllabusSlug { get; set; }
        public int Size { get; set; } = 20;
    }
}
