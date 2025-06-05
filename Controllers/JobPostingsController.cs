using HireSphere.Data;
using HireSphere.Models;
using HireSphere.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HireSphere.Controllers
{
    [Authorize(Roles = "Administrator,Recruiter")]
    public class JobPostingsController : Controller
    {
        private readonly HireSphereDbContext _context;
        private readonly FileUploadService _fileUploadService;
        private readonly AIService _aiService;
        private readonly ILogger<JobPostingsController> _logger;

        public JobPostingsController(
            HireSphereDbContext context,
            FileUploadService fileUploadService,
            AIService aiService,
            ILogger<JobPostingsController> logger)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _aiService = aiService;
            _logger = logger;
        }

        // GET: JobPostings
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            return View(await _context.JobPostings.ToListAsync());
        }

        // GET: JobPostings/Details/5
        [AllowAnonymous]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var jobPosting = await _context.JobPostings
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jobPosting == null)
            {
                return NotFound();
            }

            return View(jobPosting);
        }

        // GET: JobPostings/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: JobPostings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description,Requirements,Salary,Location")] JobPosting jobPosting)
        {
            if (ModelState.IsValid)
            {
                jobPosting.PostedDate = DateTime.Now;
                _context.Add(jobPosting);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(jobPosting);
        }

        // GET: JobPostings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var jobPosting = await _context.JobPostings.FindAsync(id);
            if (jobPosting == null)
            {
                return NotFound();
            }
            return View(jobPosting);
        }

        // POST: JobPostings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Requirements,Salary,Location,PostedDate")] JobPosting jobPosting)
        {
            if (id != jobPosting.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(jobPosting);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!JobPostingExists(jobPosting.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(jobPosting);
        }

        // GET: JobPostings/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var jobPosting = await _context.JobPostings
                .FirstOrDefaultAsync(m => m.Id == id);
            if (jobPosting == null)
            {
                return NotFound();
            }

            return View(jobPosting);
        }

        // POST: JobPostings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var jobPosting = await _context.JobPostings.FindAsync(id);
            _context.JobPostings.Remove(jobPosting);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool JobPostingExists(int id)
        {
            return _context.JobPostings.Any(e => e.Id == id);
        }

        // GET: JobPostings/Matches/5
        [HttpGet("JobPostings/Matches/{id}")]
        public async Task<IActionResult> JobDetailsWithMatches(int id)
        {
            var jobPosting = await _context.JobPostings
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jobPosting == null)
            {
                return NotFound();
            }

            try
            {
                // Get top 5 candidates with best matches
                var candidates = await _context.Candidates
                    .Include(c => c.Applications)
                    .Where(c => c.ResumePath != null)
                    .Take(5)
                    .ToListAsync();

                var candidateMatches = new List<CandidateMatch>();

                foreach (var candidate in candidates)
                {
                    try
                    {
                        var resumeText = await _fileUploadService.ExtractTextFromResumeAsync(candidate.ResumePath);
                        var matchResult = await _aiService.AnalyzeJobMatch(
                            jobPosting.Description + " " + jobPosting.Requirements,
                            resumeText);

                        candidateMatches.Add(new CandidateMatch
                        {
                            Candidate = candidate,
                            MatchPercentage = matchResult.MatchPercentage,
                            MatchingSkills = matchResult.MatchingSkills,
                            MissingSkills = matchResult.MissingSkills
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing candidate {CandidateId}", candidate.Id);
                        // Continue with next candidate if one fails
                    }
                }

                ViewBag.CandidateMatches = candidateMatches
                    .OrderByDescending(m => m.MatchPercentage)
                    .ToList();

                return View("Details", jobPosting);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting job matches");
                // Return regular details view if matching fails
                return View("Details", jobPosting);
            }
        }
    }

    public class CandidateMatch
    {
        public Candidate? Candidate { get; set; }
        public double MatchPercentage { get; set; }
        public List<string>? MatchingSkills { get; set; }
        public List<string>? MissingSkills { get; set; }
    }
}