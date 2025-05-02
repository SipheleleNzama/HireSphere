// Controllers/DashboardController.cs
using HireSphere.Data;
using HireSphere.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireSphere.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                TotalJobs = await _context.JobPostings.CountAsync(),
                ActiveJobs = await _context.JobPostings
                    .CountAsync(j => j.IsActive && j.ExpiryDate >= DateTime.Today),
                TotalCandidates = await _context.Candidates.CountAsync(),
                TotalApplications = await _context.Applications.CountAsync()
            };

            return View(model);
        }
    }
}