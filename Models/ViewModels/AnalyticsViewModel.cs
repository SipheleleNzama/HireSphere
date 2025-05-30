namespace HireSphere.Models.ViewModels
{
    public class AnalyticsDashboardViewModel
    {
        public int TotalJobs { get; set; }
        public int TotalCandidates { get; set; }
        public int TotalApplications { get; set; }
        public double AverageMatchScore { get; set; }
    }

    public class JobTrendViewModel
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
    }

    public class CandidateAnalysisViewModel
    {
        public Candidate? Candidate { get; set; }
        public List<string>? Skills { get; set; }
        public int ApplicationCount { get; set; }
        public decimal AverageMatchScore { get; set; }
    }

    public class SystemMetricsViewModel
    {
        public int UserCount { get; set; }
        public List<RoleMetricViewModel>? Roles { get; set; }
    }

    public class RoleMetricViewModel
    {
        public string? RoleName { get; set; }
        public int UserCount { get; set; }
    }
}