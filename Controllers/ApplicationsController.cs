using HireSphere.Data;
using HireSphere.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;

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
    }
}