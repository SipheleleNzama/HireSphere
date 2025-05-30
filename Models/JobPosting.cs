using HireSphere.Services;
using HireSphere.Models.AI;
using System.ComponentModel.DataAnnotations.Schema;

namespace HireSphere.Models
{
    public class JobPosting
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Requirements { get; set; }
        public string? Location { get; set; }
        public decimal SalaryRangeMin { get; set; }
        public decimal SalaryRangeMax { get; set; }
        public DateTime PostedDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public ICollection<Application>? Applications { get; set; }

        [NotMapped] // This won't be stored in database
        public MatchAnalysisResult? MatchAnalysis { get; set; }
    }
}
