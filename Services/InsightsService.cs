using System.Globalization;
using HireSphere.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HireSphere.Models.AI;

namespace HireSphere.Services
{
    // Services/InsightsService.cs
    public class InsightsService
    {
        private readonly HireSphereDbContext _context;

        public InsightsService(HireSphereDbContext context)
        {
            _context = context;
        }

        // Get top N skills across all candidates
        public Dictionary<string, int> GetTopSkills(int topN = 10)
        {
            return _context.Candidates
                .Where(c => c.Skills != null)
                .SelectMany(c => c.Skills.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(skill => skill.Trim().ToLower())
                .GroupBy(skill => skill)
                .OrderByDescending(g => g.Count())
                .Take(topN)
                .ToDictionary(g => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(g.Key), g => g.Count());
        }

        // Compare candidate expectations vs job offers
        public object GetSalaryAlignmentStats()
        {
            var data = _context.JobPostings
                .Where(j => j.IsActive)
                .SelectMany(j => j.Applications)
                .Select(a => new
                {
                    JobTitle = a.JobPosting.Title,
                    JobMinSalary = a.JobPosting.SalaryRangeMin,
                    JobMaxSalary = a.JobPosting.SalaryRangeMax,
                    CandidateExpected = a.Candidate.ExpectedSalary,
                    CandidateName = $"{a.Candidate.FirstName} {a.Candidate.LastName}"
                })
                .ToList();

            return new
            {
                UnderBudget = data.Count(d => d.CandidateExpected < d.JobMinSalary),
                InRange = data.Count(d => d.CandidateExpected >= d.JobMinSalary && d.CandidateExpected <= d.JobMaxSalary),
                OverBudget = data.Count(d => d.CandidateExpected > d.JobMaxSalary),
                RawData = data.OrderBy(d => d.JobTitle).Take(50) // Sample for chart
            };
        }
    }
}
