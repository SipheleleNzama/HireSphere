namespace HireSphere.Models
{
    public class ApplicationNote
    {
        public int Id { get; set; }
        public string? Content { get; set; }
        public string? AuthorId { get; set; } // Recruiter's User ID
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int ApplicationId { get; set; }
        public Application? Application { get; set; }
    }
}
