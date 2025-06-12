using HireSphere.Data;
using HireSphere.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore.Infrastructure;


namespace HireSphere.Controllers
{
    public class ApplicationsController : Controller
    {
        private readonly HireSphereDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ApplicationsController(HireSphereDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Applications/Create/{jobId}
        [AllowAnonymous]
        public IActionResult Create(int jobId)
        {
            var job = _context.JobPostings.Find(jobId);
            if (job == null)
            {
                return NotFound();
            }

            ViewData["JobTitle"] = job.Title;
            ViewData["JobId"] = jobId;

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int jobId, [Bind("FirstName,LastName,Email,Phone,ExpectedSalary")] Candidate candidate, IFormFile resumeFile)
        {
            if (ModelState.IsValid)
            {
                // Save resume file
                if (resumeFile != null && resumeFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "resumes");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + resumeFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await resumeFile.CopyToAsync(fileStream);
                    }

                    candidate.ResumePath = "/resumes/" + uniqueFileName;
                }

                // Save candidate
                _context.Candidates.Add(candidate);
                await _context.SaveChangesAsync();

                // Create application
                var application = new Application
                {
                    JobPostingId = jobId,
                    CandidateId = candidate.Id,
                    ApplicationDate = DateTime.Now,
                    LastUpdated = DateTime.Now,
                    Status = ApplicationStatus.Submitted,
                    ResumePath = candidate.ResumePath // Now this will work
                };

                _context.Applications.Add(application);
                await _context.SaveChangesAsync();

                return RedirectToAction("Confirmation", new { id = application.Id });
            }

            var job = _context.JobPostings.Find(jobId);
            ViewData["JobTitle"] = job.Title;
            ViewData["JobId"] = jobId;

            return View(candidate);
        }

        [AllowAnonymous]
        public IActionResult Confirmation(int id)
        {
            var application = _context.Applications
                .Include(a => a.JobPosting)
                .Include(a => a.Candidate)
                .FirstOrDefault(a => a.Id == id);

            if (application == null)
            {
                return NotFound();
            }

            return View(application);
        }

        [HttpPost]
        [Authorize(Roles = "Recruiter,Administrator")]
        public async Task<IActionResult> UpdateStatus(int applicationId, ApplicationStatus status, string? feedback = null)
        {
            var application = await _context.Applications.FindAsync(applicationId);
            if (application == null) return NotFound();

            application.Status = status;
            application.Feedback = feedback;
            application.LastUpdated = DateTime.Now; // Now this will work

            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> BulkUpdateStatus(
    [FromBody] List<int> applicationIds,
    ApplicationStatus newStatus)
        {
            var applications = await _context.Applications
                .Where(a => applicationIds.Contains(a.Id))
                .ToListAsync();

            foreach (var app in applications)
            {
                app.Status = newStatus;
                app.LastUpdated = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}