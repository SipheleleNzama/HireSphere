namespace HireSphere.Models
{
    public class Candidate
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? ResumePath { get; set; }
        public string? Skills { get; set; }
        public decimal ExpectedSalary { get; set; }
        public ICollection<Application>? Applications { get; set; }
    }
}
