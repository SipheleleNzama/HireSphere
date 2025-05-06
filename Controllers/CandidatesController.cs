using HireSphere.Data;
using HireSphere.Models;
using HireSphere.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;



namespace HireSphere.Controllers
{
    public class CandidatesController : Controller
    {
        private readonly HireSphereDbContext _context;
        private readonly FileUploadService _fileUploadService;
        private readonly AIService _aiService;

        public CandidatesController(
            HireSphereDbContext context,
            FileUploadService fileUploadService,
            AIService aiService)
        {
            _context = context;
            _fileUploadService = fileUploadService;
            _aiService = aiService;
        }

        // GET: Candidates
        public async Task<IActionResult> Index(string searchString, string sortOrder)
        {
            ViewData["NameSortParm"] = string.IsNullOrEmpty(sortOrder) ? "name_desc" : "";
            ViewData["DateSortParm"] = sortOrder == "Date" ? "date_desc" : "Date";
            ViewData["CurrentFilter"] = searchString;

            var candidates = from c in _context.Candidates
                             select c;

            if (!string.IsNullOrEmpty(searchString))
            {
                candidates = candidates.Where(c =>
                    c.FirstName.Contains(searchString) ||
                    c.LastName.Contains(searchString) ||
                    c.Email.Contains(searchString) ||
                    c.Skills.Contains(searchString));
            }

            candidates = sortOrder switch
            {
                "name_desc" => candidates.OrderByDescending(c => c.LastName),
                "Date" => candidates.OrderBy(c => c.Applications.Max(a => a.ApplicationDate)),
                "date_desc" => candidates.OrderByDescending(c => c.Applications.Max(a => a.ApplicationDate)),
                _ => candidates.OrderBy(c => c.LastName),
            };

            return View(await candidates.AsNoTracking().ToListAsync());
        }

        // GET: Candidates/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Candidates/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,FirstName,LastName,Email,Phone,Skills,ExpectedSalary")] Candidate candidate, IFormFile resumeFile)
        {
            if (ModelState.IsValid)
            {
                if (resumeFile != null && resumeFile.Length > 0)
                {
                    try
                    {
                        // Upload resume
                        var resumePath = await _fileUploadService.UploadResumeAsync(resumeFile, candidate.Id);
                        candidate.ResumePath = resumePath;

                        // Extract skills from resume if Skills field is empty
                        if (string.IsNullOrEmpty(candidate.Skills))
                        {
                            var resumeText = await _fileUploadService.ExtractTextFromResumeAsync(resumePath);
                            var skills = await _aiService.ExtractKeyPhrasesAsync(resumeText);
                            candidate.Skills = string.Join(", ", skills.Keys);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("resumeFile", $"Error processing resume: {ex.Message}");
                        return View(candidate);
                    }
                }

                _context.Add(candidate);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(candidate);
        }

        // GET: Candidates/View/5
        public async Task<IActionResult> View(int id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var candidate = await _context.Candidates
                 .Include(c => c.Applications)
                 .ThenInclude(a => a.JobPosting)
                 .FirstOrDefaultAsync(m => m.Id == id);

            if (candidate == null)
            {
                return NotFound();
            } 

            // Get AI analysis if resume exists
            if (!string.IsNullOrEmpty(candidate.ResumePath))
            {
                try
                {
                    var resumeText = await _fileUploadService.ExtractTextFromResumeAsync(candidate.ResumePath);
                    ViewBag.ResumeAnalysis = await _aiService.AnalyzeResume(resumeText);
                }
                catch
                {
                    // Analysis failed, continue without it
                }
            }

            return View(candidate);
        }

        // POST: Candidates/Apply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Apply(int jobId, IFormFile resume, Candidate candidate)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Save candidate
                    _context.Candidates.Add(candidate);
                    await _context.SaveChangesAsync();

                    // Upload resume
                    var resumePath = await _fileUploadService.UploadResumeAsync(resume, candidate.Id);
                    candidate.ResumePath = resumePath;
                    _context.Update(candidate);

                    // Extract skills from resume
                    var resumeText = await _fileUploadService.ExtractTextFromResumeAsync(resumePath);
                    candidate.Skills = string.Join(", ", (await _aiService.ExtractKeyPhrasesAsync(resumeText)).Keys);

                    // Create application
                    var job = await _context.JobPostings.FindAsync(jobId);
                    if (job == null)
                    {
                        return NotFound();
                    }

                    var application = new Application
                    {
                        JobPostingId = jobId,
                        CandidateId = candidate.Id,
                        ApplicationDate = DateTime.Now,
                        Status = ApplicationStatus.Submitted,
                        MatchScore = (decimal)await _aiService.CalculateMatchScore(
                            job.Description + " " + job.Requirements,
                            resumeText)
                    };

                    _context.Applications.Add(application);
                    await _context.SaveChangesAsync();

                    return RedirectToAction("ApplicationSuccess", new { applicationId = application.Id });
                }

                return RedirectToAction("Details", "JobPostings", new { id = jobId });
            }
            catch (Exception ex)
            {
                // Log the error
                ModelState.AddModelError("", "There was an error processing your application. Please try again.");
                return RedirectToAction("Details", "JobPostings", new { id = jobId });
            }
        }

        // GET: Candidates/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var candidate = await _context.Candidates.FindAsync(id);
            if (candidate == null)
            {
                return NotFound();
            }
            return View(candidate);
        }

        // POST: Candidates/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FirstName,LastName,Email,Phone,Skills,ExpectedSalary,ResumePath")] Candidate candidate, IFormFile resumeFile)
        {
            if (id != candidate.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (resumeFile != null && resumeFile.Length > 0)
                    {
                        // Delete old resume if exists
                        if (!string.IsNullOrEmpty(candidate.ResumePath))
                        {
                            await _fileUploadService.DeleteResumeAsync(candidate.ResumePath);
                        }

                        // Upload new resume
                        var resumePath = await _fileUploadService.UploadResumeAsync(resumeFile, candidate.Id);
                        candidate.ResumePath = resumePath;
                    }

                    _context.Update(candidate);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CandidateExists(candidate.Id))
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
            return View(candidate);
        }

        // GET: Candidates/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var candidate = await _context.Candidates
                .FirstOrDefaultAsync(m => m.Id == id);
            if (candidate == null)
            {
                return NotFound();
            }

            return View(candidate);
        }

        // POST: Candidates/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var candidate = await _context.Candidates.FindAsync(id);
            if (candidate != null)
            {
                // Delete associated resume
                if (!string.IsNullOrEmpty(candidate.ResumePath))
                {
                    await _fileUploadService.DeleteResumeAsync(candidate.ResumePath);
                }

                _context.Candidates.Remove(candidate);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool CandidateExists(int id)
        {
            return _context.Candidates.Any(e => e.Id == id);
        }
    }
}