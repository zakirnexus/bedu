using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SchoolProject.Models.Lookups
{
    [Table("tb_nsewc")]
    public class Nsewc
    {
        [Key]
        [Column("nsewc_id")]
        public int NsewcId { get; set; }

        [Column("nsewc_name")]
        public string? NsewcName { get; set; }

        public ICollection<SchoolProject.Models.School>? Schools { get; set; }
        public ICollection<Locality>? Localities { get; set; }
    }
}
