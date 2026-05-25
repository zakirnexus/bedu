namespace SchoolProject.Models.Search
{
    public class SchoolSearchRequest
    {
        public string Q { get; set; }
        public int? CityId { get; set; }
        public int? LocalityId { get; set; }
        public int? CoedId { get; set; }
        public int? OwnershipId { get; set; }
        public int? NsewcId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}