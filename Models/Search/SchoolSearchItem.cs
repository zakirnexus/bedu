namespace SchoolProject.Models.Search
{
    public class SchoolSearchItem
    {
        public string Id { get; set; }
        public string DocType { get; set; }
        public int EntityId { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Url { get; set; }
        public int LocalityId { get; set; }
        public string LocalityName { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; }
        public int NsewcId { get; set; }
        public int CoedId { get; set; }
        public int OwnershipId { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public int ListingRank { get; set; }
        public string Keywords { get; set; }
        public string Description { get; set; }
    }
}