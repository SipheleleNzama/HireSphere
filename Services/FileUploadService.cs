using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
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

        public FileUploadService(IWebHostEnvironment environment, ILogger<FileUploadService> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public async Task<string> UploadResumeAsync(IFormFile file, int candidateId)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("No file uploaded");
                }

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "resumes");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var uniqueFileName = $"{candidateId}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(fileStream);
                }

                return $"/resumes/{uniqueFileName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading resume");
                throw;
            }
        }

        public async Task<string> ExtractTextFromResumeAsync(string filePath)
        {
            try
            {
                var fullPath = Path.Combine(_environment.WebRootPath, filePath.TrimStart('/'));

                if (!System.IO.File.Exists(fullPath))
                {
                    throw new FileNotFoundException("Resume file not found");
                }

                var extension = Path.GetExtension(fullPath).ToLower();

                if (extension == ".pdf")
                {
                    return ExtractTextFromPdf(fullPath);
                }
                else if (extension == ".docx")
                {
                    return ExtractTextFromDocx(fullPath);
                }
                else if (extension == ".txt")
                {
                    return await System.IO.File.ReadAllTextAsync(fullPath);
                }
                else
                {
                    throw new NotSupportedException("File format not supported");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from resume");
                throw;
            }
        }

        private string ExtractTextFromPdf(string filePath)
        {
            var text = new System.Text.StringBuilder();
            using (var pdfReader = new PdfReader(filePath))
            using (var pdfDocument = new PdfDocument(pdfReader))
            {
                for (int page = 1; page <= pdfDocument.GetNumberOfPages(); page++)
                {
                    var strategy = new SimpleTextExtractionStrategy();
                    var currentText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(page), strategy);
                    text.Append(currentText);
                }
            }
            return text.ToString();
        }

        private string ExtractTextFromDocx(string filePath)
        {
            using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
            {
                var body = doc.MainDocumentPart?.Document.Body;
                return body?.InnerText ?? string.Empty;
            }
        }
    }
}