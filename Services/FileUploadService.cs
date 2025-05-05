using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace HireSphere.Services
{
    public class FileUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private readonly long _maxFileSize;

        public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger, IConfiguration configuration)
        {
            _environment = environment;
            _logger = logger;
            _maxFileSize = configuration.GetValue<long>("FileUpload:MaxSizeBytes", 5 * 1024 * 1024); // Default 5MB
        }

        public async Task<string> UploadResumeAsync(IFormFile file, int candidateId)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("No file was selected for upload.");
                }

                if (file.Length > _maxFileSize)
                {
                    throw new ArgumentException($"File size exceeds the maximum limit of {_maxFileSize / (1024 * 1024)}MB.");
                }

                var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                {
                    throw new ArgumentException("Only PDF, DOCX, and TXT files are allowed.");
                }

                // Create candidate-specific directory
                var candidateFolder = Path.Combine(_environment.WebRootPath, "resumes", candidateId.ToString());
                Directory.CreateDirectory(candidateFolder);

                // Generate secure filename
                var sanitizedFileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Path.GetFileNameWithoutExtension(file.FileName)}";
                sanitizedFileName = string.Join("_", sanitizedFileName.Split(Path.GetInvalidFileNameChars()));
                var uniqueFileName = $"{sanitizedFileName}{fileExtension}";
                var filePath = Path.Combine(candidateFolder, uniqueFileName);

                // Save file
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                // Set permissions (Unix systems)
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(filePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite |
                        UnixFileMode.GroupRead |
                        UnixFileMode.OtherRead);
                }

                return $"/resumes/{candidateId}/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading resume for candidate {CandidateId}", candidateId);
                throw new ApplicationException("Failed to upload resume", ex);
            }
        }

        public async Task<string> ExtractTextFromResumeAsync(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));

                if (!System.IO.File.Exists(fullPath))
                {
                    throw new FileNotFoundException("Resume file not found", fullPath);
                }

                var extension = Path.GetExtension(fullPath).ToLowerInvariant();

                return extension switch
                {
                    ".pdf" => ExtractTextFromPdf(fullPath),
                    ".docx" => ExtractTextFromDocx(fullPath),
                    ".txt" => await System.IO.File.ReadAllTextAsync(fullPath),
                    _ => throw new NotSupportedException($"File extension '{extension}' is not supported for text extraction.")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from resume at {FilePath}", filePath);
                throw new ApplicationException("Failed to extract text from resume", ex);
            }
        }

        private string ExtractTextFromPdf(string filePath)
        {
            try
            {
                var text = new System.Text.StringBuilder();

                using (var pdfReader = new PdfReader(filePath))
                using (var pdfDocument = new PdfDocument(pdfReader))
                {
                    var strategy = new LocationTextExtractionStrategy();

                    for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
                    {
                        var currentText = PdfTextExtractor.GetTextFromPage(
                            pdfDocument.GetPage(page),
                            strategy);
                        text.AppendLine(currentText);
                    }
                }

                return text.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF at {FilePath}", filePath);
                throw new ApplicationException("Failed to extract text from PDF", ex);
            }
        }

        private string ExtractTextFromDocx(string filePath)
        {
            try
            {
                using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
                {
                    var body = doc.MainDocumentPart?.Document.Body;
                    if (body == null) return string.Empty;

                    return string.Join(Environment.NewLine,
                        body.Descendants<Paragraph>()
                            .Select(p => p.InnerText));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from DOCX at {FilePath}", filePath);
                throw new ApplicationException("Failed to extract text from DOCX", ex);
            }
        }

        public async Task DeleteResumeAsync(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return;

                var physicalPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));
                if (System.IO.File.Exists(physicalPath))
                {
                    System.IO.File.Delete(physicalPath);

                    // Try to delete the parent directory if empty
                    var directory = Path.GetDirectoryName(physicalPath);
                    if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting resume at {FilePath}", filePath);
                throw new ApplicationException("Failed to delete resume file", ex);
            }
        }
    }
}