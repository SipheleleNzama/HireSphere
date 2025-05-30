
namespace HireSphere.Models.AI
{
    public class MatchAnalysisResult
    {
        public double MatchPercentage { get; set; }
        public List<string> MatchingSkills { get; set; } = new();
        public List<string> MissingSkills { get; set; } = new();
        public List<string> AdditionalSkills { get; set; } = new();
    }
}