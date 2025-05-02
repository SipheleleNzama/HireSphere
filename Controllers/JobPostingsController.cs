using HireSphere.Data;
using HireSphere.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HireSphere.Controllers
{
  
    public class JobPostingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<JobPostingsController> _logger;

        public JobPostingsController(ApplicationDbContext context, ILogger<JobPostingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var jobs = await _context.JobPostings
                .Where(j => j.IsActive && j.ExpiryDate >= DateTime.Today)
                .OrderByDescending(j => j.PostedDate)
                .ToListAsync();

            return View(jobs);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(JobPosting model)
        {
            if (ModelState.IsValid)
            {
                model.PostedDate = DateTime.Now;
                model.IsActive = true;

                _context.Add(model);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var job = await _context.JobPostings
                .Include(j => j.Applications)
                .ThenInclude(a => a.Candidate)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (job == null)
            {
                return NotFound();
            }

            return View(job);
        }
    }
}
