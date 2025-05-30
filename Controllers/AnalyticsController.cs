using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HireSphere.Data;
using HireSphere.Models;
using HireSphere.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using HireSphere.Models.ViewModels;

namespace HireSphere.Controllers
{
    [Authorize(Roles = "DataAnalyst,Administrator")]
    public class AnalyticsController : Controller
    {
        private readonly HireSphereDbContext _context;
        private readonly AIService _aiService;
        private readonly FileUploadService _fileUploadService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            HireSphereDbContext context,
            AIService aiService,
            FileUploadService fileUploadService,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ILogger<AnalyticsController> logger)
        {
            _context = context;
            _aiService = aiService;
            _fileUploadService = fileUploadService;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var model = new AnalyticsDashboardViewModel
            {
                TotalJobs = await _context.JobPostings.CountAsync(),
                TotalCandidates = await _context.Candidates.CountAsync(),
                TotalApplications = await _context.Applications.CountAsync(),
                AverageMatchScore = await _context.Applications.AverageAsync(a => (double?)a.MatchScore) ?? 0
            };

            return View(model);
        }

        public async Task<IActionResult> JobTrends()
        {
            var trends = await _context.JobPostings
                .GroupBy(j => j.PostedDate.Date)
                .Select(g => new JobTrendViewModel
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(t => t.Date)
                .ToListAsync();

            return View(trends);
        }

        public async Task<IActionResult> CandidateAnalysis()
        {
            var candidates = await _context.Candidates
                .Include(c => c.Applications)
                .Take(50)
                .ToListAsync();

            var analysis = new List<CandidateAnalysisViewModel>();

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate.ResumePath))
                {
                    try
                    {
                        var resumeText = await _fileUploadService.ExtractTextFromResumeAsync(candidate.ResumePath);
                        var skills = await _aiService.ExtractKeyPhrasesAsync(resumeText);

                        analysis.Add(new CandidateAnalysisViewModel
                        {
                            Candidate = candidate,
                            Skills = skills.Keys.ToList(),
                            ApplicationCount = candidate.Applications.Count,
                            AverageMatchScore = candidate.Applications.Any() ?
                                candidate.Applications.Average(a => a.MatchScore) : 0
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error analyzing candidate {CandidateId}", candidate.Id);
                    }
                }
            }

            return View(analysis);
        }

        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> SystemMetrics()
        {
            var metrics = new SystemMetricsViewModel
            {
                UserCount = await _userManager.Users.CountAsync(),
                Roles = await _roleManager.Roles
                    .Select(r => new RoleMetricViewModel
                    {
                        RoleName = r.Name,
                        UserCount = _userManager.GetUsersInRoleAsync(r.Name).Result.Count
                    })
                    .ToListAsync()
            };

            return View(metrics);
        }
    }
}