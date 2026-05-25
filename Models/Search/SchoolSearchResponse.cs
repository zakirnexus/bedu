namespace SchoolProject.Models.Search
{
    public class SchoolSearchResponse
    {
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public List<SchoolSearchItem> Items { get; set; }
    }
}