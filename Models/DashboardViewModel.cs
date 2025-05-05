namespace HireSphere.Models
{
    public class DashboardViewModel
    {
        public int TotalJobs { get; set; }
        public int ActiveJobs { get; set; }
        public int TotalCandidates { get; set; }
        public int TotalApplications { get; set; }

        // For Applications Trend Chart
        public List<string>? ApplicationTrendLabels { get; set; }
        public List<int>? CurrentApplicationTrendData { get; set; }
        public List<int>? PreviousApplicationTrendData { get; set; }

        // For Job Status Chart
        public int ActiveJobsCount { get; set; }
        public int ClosedJobsCount { get; set; }
        public int DraftJobsCount { get; set; }

        public int ActiveJobsPercentage => (int)Math.Round((double)ActiveJobsCount / (ActiveJobsCount + ClosedJobsCount + DraftJobsCount) * 100);
        public int ClosedJobsPercentage => (int)Math.Round((double)ClosedJobsCount / (ActiveJobsCount + ClosedJobsCount + DraftJobsCount) * 100);
        public int DraftJobsPercentage => (int)Math.Round((double)DraftJobsCount / (ActiveJobsCount + ClosedJobsCount + DraftJobsCount) * 100);

    }
}
