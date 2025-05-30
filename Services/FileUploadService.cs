using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Polly;
using Polly.Retry;
using System.Linq;

namespace HireSphere.Services
{
    public class FileUploadService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<FileUploadService> _logger;
        private readonly long _maxFileSize;
        private readonly TimeSpan _fileProcessTimeout = TimeSpan.FromSeconds(30);

        private static readonly AsyncRetryPolicy RetryPolicy = Policy
            .Handle<IOException>()
            .WaitAndRetryAsync(3, retryAttempt =>
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

        public FileUploadService(IWebHostEnvironment environment,
                               ILogger<FileUploadService> logger,
                               IConfiguration configuration)
        {
            _environment = environment;
            _logger = logger;
            _maxFileSize = configuration.GetValue<long>("FileUpload:MaxSizeBytes", 5 * 1024 * 1024);

            // Ensure resumes directory exists
            var resumesDir = Path.Combine(_environment.WebRootPath, "resumes");
            if (!Directory.Exists(resumesDir))
            {
                Directory.CreateDirectory(resumesDir);
                _logger.LogInformation("Created resumes directory at {ResumesPath}", resumesDir);
            }
        }


        public async Task<string> UploadResumeAsync(IFormFile file, int candidateId)
        {
            _logger.LogInformation("Starting file upload for candidate {CandidateId}", candidateId);

            using var timeoutCts = new CancellationTokenSource(_fileProcessTimeout);

            try
            {
                _logger.LogInformation("Validating file for candidate {CandidateId}", candidateId);

                // Validate file
                if (file == null || file.Length == 0)
                {
                    _logger.LogWarning("No file selected for candidate {CandidateId}", candidateId);
                    throw new ArgumentException("No file was selected");
                }

                _logger.LogInformation("File size: {FileSize} bytes for candidate {CandidateId}", file.Length, candidateId);

                if (file.Length > _maxFileSize)
                {
                    _logger.LogWarning("File too large ({FileSize} bytes) for candidate {CandidateId}", file.Length, candidateId);
                    throw new ArgumentException($"File size exceeds {_maxFileSize / 1024 / 1024}MB limit");
                }

                _logger.LogInformation("Checking file extension for candidate {CandidateId}", candidateId);

                var allowedExtensions = new[] { ".pdf", ".docx", ".txt" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                _logger.LogInformation("File extension: {FileExtension} for candidate {CandidateId}", fileExtension, candidateId);

                if (!allowedExtensions.Contains(fileExtension))
                {
                    _logger.LogWarning("Invalid file extension {FileExtension} for candidate {CandidateId}", fileExtension, candidateId);
                    throw new ArgumentException("Only PDF, DOCX and TXT files are allowed");
                }

                _logger.LogInformation("Creating candidate folder for candidate {CandidateId}", candidateId);

                // Create candidate-specific folder
                var candidateFolder = Path.Combine(_environment.WebRootPath, "resumes", candidateId.ToString());

                _logger.LogInformation("Candidate folder path: {CandidateFolder} for candidate {CandidateId}", candidateFolder, candidateId);

                try
                {
                    _logger.LogInformation("Creating directory for candidate {CandidateId}", candidateId);
                    Directory.CreateDirectory(candidateFolder);
                    _logger.LogInformation("Directory created successfully for candidate {CandidateId}", candidateId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create directory {Directory} for candidate {CandidateId}", candidateFolder, candidateId);
                    throw new ApplicationException("Failed to create storage directory", ex);
                }

                // Generate safe filename
                var safeFileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(candidateFolder, safeFileName);

                _logger.LogInformation("Generated file path: {FilePath} for candidate {CandidateId}", filePath, candidateId);

                // Save file with retry logic
                _logger.LogInformation("Starting file save for candidate {CandidateId}", candidateId);
                await RetryPolicy.ExecuteAsync(async () =>
                {
                    _logger.LogInformation("Creating file stream for candidate {CandidateId}", candidateId);
                    await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None,
                        bufferSize: 4096, useAsync: true);

                    _logger.LogInformation("Starting file copy for candidate {CandidateId}", candidateId);
                    await file.CopyToAsync(stream, timeoutCts.Token);
                    _logger.LogInformation("File copy completed for candidate {CandidateId}", candidateId);
                });

                _logger.LogInformation("Successfully uploaded resume for candidate {CandidateId} to {FilePath}",
                    candidateId, filePath);

                return $"/resumes/{candidateId}/{safeFileName}";
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("File upload timed out for candidate {CandidateId}", candidateId);
                throw new ApplicationException("File upload operation timed out");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading resume for candidate {CandidateId}: {ErrorMessage}",
                    candidateId, ex.ToString());
                throw new ApplicationException($"Failed to upload resume: {ex.Message}", ex);
            }
        }

        public async Task<string> ExtractTextFromResumeAsync(string filePath)
        {
            using var timeoutCts = new CancellationTokenSource(_fileProcessTimeout);

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
                    ".pdf" => await Task.Run(() => ExtractTextFromPdf(fullPath), timeoutCts.Token),
                    ".docx" => await Task.Run(() => ExtractTextFromDocx(fullPath), timeoutCts.Token),
                    ".txt" => await System.IO.File.ReadAllTextAsync(fullPath, timeoutCts.Token),
                    _ => throw new NotSupportedException($"File extension '{extension}' is not supported")
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogError("Text extraction timed out for {FilePath}", filePath);
                throw new ApplicationException("Text extraction timed out");
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
                throw new ApplicationException("Failed to process PDF file", ex);
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
                throw new ApplicationException("Failed to process Word document", ex);
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
                    await Task.Run(() =>
                    {
                        System.IO.File.Delete(physicalPath);

                        // Try to delete the parent directory if empty
                        var directory = Path.GetDirectoryName(physicalPath);
                        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                        {
                            Directory.Delete(directory);
                        }
                    });
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