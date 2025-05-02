namespace HireSphere.Models
{
    public class Application
    {
        public int Id { get; set; }
        public int JobPostingId { get; set; }
        public JobPosting? JobPosting { get; set; }
        public int CandidateId { get; set; }
        public Candidate? Candidate { get; set; }
        public DateTime ApplicationDate { get; set; }
        public ApplicationStatus? Status { get; set; }
        public string? ResumePath { get; set; }
        public decimal MatchScore { get; set; }
        public string? AIEvaluationNotes { get; set; }
    }
}
