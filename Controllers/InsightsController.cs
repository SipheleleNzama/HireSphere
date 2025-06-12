using HireSphere.Data;
using HireSphere.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HireSphere.Controllers
{
    [Route("[controller]")]
    public class InsightsController : Controller
    {
        private readonly HireSphereDbContext _context;

        public InsightsController(HireSphereDbContext context)
        {
            _context = context;
        }

        // Return view for /Insights
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("top-skills")]
        public IActionResult GetTopSkills()
        {
            // First, load candidates with skills into memory
            var candidatesWithSkills = _context.Candidates
                .Where(c => c.Skills != null && c.Skills != "")
                .Select(c => c.Skills)
                .ToList(); // This executes the query and brings data to memory

            // Now perform string operations in memory
            var skills = candidatesWithSkills
                .SelectMany(skillString => skillString.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(skill => skill.Trim().ToLower())
                .Where(skill => !string.IsNullOrWhiteSpace(skill))
                .GroupBy(skill => skill)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(g.Key), g => g.Count());

            return Json(skills);
        }

        [HttpGet("salary-alignment")]
        public IActionResult GetSalaryAlignment()
        {
            var data = _context.JobPostings
                .Include(j => j.Applications)
                .ThenInclude(a => a.Candidate)
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

            var result = new
            {
                UnderBudget = data.Count(d => d.CandidateExpected < d.JobMinSalary),
                InRange = data.Count(d => d.CandidateExpected >= d.JobMinSalary &&
                                        d.CandidateExpected <= d.JobMaxSalary),
                OverBudget = data.Count(d => d.CandidateExpected > d.JobMaxSalary),
                RawData = data.OrderBy(d => d.JobTitle).Take(50)
            };

            return Json(result);
        }

        [HttpGet("application-trends")]
        public IActionResult GetApplicationTrends()
        {
            var trends = _context.JobPostings
                .Where(j => j.PostedDate > DateTime.Now.AddMonths(-6))
                .OrderBy(j => j.PostedDate)
                .Select(j => new
                {
                    JobTitle = j.Title,
                    PostedDate = j.PostedDate.ToString("yyyy-MM-dd"),
                    ApplicationCount = j.Applications.Count,
                    CompletionPercentage = (DateTime.Now > j.ExpiryDate)
                        ? 100
                        : (int)((DateTime.Now - j.PostedDate).TotalDays /
                              (j.ExpiryDate - j.PostedDate).TotalDays * 100)
                })
                .ToList();

            return Json(trends);
        }
    }
}