using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Areas.Admin.ViewModels;
using Project.Data;
using Project.Models;

namespace Project.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.Role_Admin)]
    public class AttractionsController : Controller
    {
        private const int MaxFileSizeBytes = 20 * 1024 * 1024;
        // One main image plus up to ten gallery images (each image max 20 MB).
        private const int MaxAdditionalImages = 10;
        // Permit a 20 MB main image plus ten 20 MB gallery images, with multipart overhead.
        private const int MaxRequestSizeBytes = 250 * 1024 * 1024;
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<AttractionsController> _logger;

        public AttractionsController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            ILogger<AttractionsController> logger)
        {
            _context = context;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var attractions = await _context.Attractions
                .AsNoTracking()
                .Include(a => a.Images)
                .OrderBy(a => a.Name)
                .ToListAsync();

            return View(attractions);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new AttractionFormViewModel { IsAvailable = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSizeBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
        public async Task<IActionResult> Create(AttractionFormViewModel model)
        {
            var additionalImages = GetSelectedFiles(model.AdditionalImages);
            ValidateImage(model.MainImage, nameof(model.MainImage), required: true);
            ValidateAdditionalImages(additionalImages);

            if (!ModelState.IsValid)
            {
                model.AdditionalImages = new List<IFormFile>();
                return View(model);
            }

            var savedImageUrls = new List<string>();
            await using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var mainImageUrl = await SaveImageAsync(model.MainImage!);
                savedImageUrls.Add(mainImageUrl);

                var additionalImageUrls = new List<string>();
                foreach (var image in additionalImages)
                {
                    var imageUrl = await SaveImageAsync(image);
                    savedImageUrls.Add(imageUrl);
                    additionalImageUrls.Add(imageUrl);
                }

                var attraction = new Attraction
                {
                    Name = model.Name.Trim(),
                    City = model.City.Trim(),
                    Description = model.Description.Trim(),
                    ImageUrl = mainImageUrl,
                    TicketPrice = model.TicketPrice,
                    IsAvailable = model.IsAvailable
                };

                _context.Attractions.Add(attraction);
                await _context.SaveChangesAsync();

                if (additionalImageUrls.Count > 0)
                {
                    var galleryImages = additionalImageUrls.Select(url => new AttractionImage
                    {
                        AttractionId = attraction.Id,
                        ImageUrl = url
                    });

                    _context.AttractionImages.AddRange(galleryImages);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                TempData["Success"] = "Attraction created successfully. It can now appear in Attraction Tickets search when it is available.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create an attraction.");
                foreach (var imageUrl in savedImageUrls)
                {
                    DeleteUploadedImage(imageUrl);
                }

                ModelState.AddModelError(string.Empty, "The attraction could not be saved. Please check the form and try again.");
                model.AdditionalImages = new List<IFormFile>();
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var attraction = await _context.Attractions
                .AsNoTracking()
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attraction == null)
            {
                return NotFound();
            }

            return View(ToFormViewModel(attraction));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxRequestSizeBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestSizeBytes)]
        public async Task<IActionResult> Edit(int id, AttractionFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var attraction = await _context.Attractions
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attraction == null)
            {
                return NotFound();
            }

            var additionalImages = GetSelectedFiles(model.AdditionalImages);
            ValidateImage(model.MainImage, nameof(model.MainImage), required: false);
            ValidateAdditionalImages(additionalImages);

            var requestedRemovalIds = model.RemoveImageIds?.Distinct().ToHashSet() ?? new HashSet<int>();
            var matchingRemovals = attraction.Images
                .Where(i => requestedRemovalIds.Contains(i.Id))
                .ToList();
            var remainingGalleryCount = attraction.Images.Count - matchingRemovals.Count;

            if (remainingGalleryCount + additionalImages.Count > MaxAdditionalImages)
            {
                ModelState.AddModelError(nameof(model.AdditionalImages), $"An attraction can have up to {MaxAdditionalImages} additional images. Remove an existing image before adding more.");
            }

            if (!ModelState.IsValid)
            {
                model.ExistingImages = attraction.Images
                    .Select(i => new ExistingAttractionImageViewModel { Id = i.Id, ImageUrl = i.ImageUrl })
                    .ToList();
                model.CurrentMainImageUrl = attraction.ImageUrl;
                model.AdditionalImages = new List<IFormFile>();
                return View(model);
            }

            var savedImageUrls = new List<string>();
            var imageUrlsToDelete = matchingRemovals.Select(i => i.ImageUrl).ToList();
            var oldMainImageUrl = attraction.ImageUrl;

            try
            {
                if (model.MainImage != null && model.MainImage.Length > 0)
                {
                    attraction.ImageUrl = await SaveImageAsync(model.MainImage);
                    savedImageUrls.Add(attraction.ImageUrl);
                    imageUrlsToDelete.Add(oldMainImageUrl);
                }

                foreach (var image in additionalImages)
                {
                    var imageUrl = await SaveImageAsync(image);
                    savedImageUrls.Add(imageUrl);
                    _context.AttractionImages.Add(new AttractionImage
                    {
                        AttractionId = attraction.Id,
                        ImageUrl = imageUrl
                    });
                }

                attraction.Name = model.Name.Trim();
                attraction.City = model.City.Trim();
                attraction.Description = model.Description.Trim();
                attraction.TicketPrice = model.TicketPrice;
                attraction.IsAvailable = model.IsAvailable;

                if (matchingRemovals.Count > 0)
                {
                    _context.AttractionImages.RemoveRange(matchingRemovals);
                }

                await _context.SaveChangesAsync();

                foreach (var imageUrl in imageUrlsToDelete)
                {
                    DeleteUploadedImage(imageUrl);
                }

                TempData["Success"] = "Attraction updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update attraction {AttractionId}.", id);
                foreach (var imageUrl in savedImageUrls)
                {
                    DeleteUploadedImage(imageUrl);
                }

                ModelState.AddModelError(string.Empty, "The attraction could not be updated. Please try again.");
                model.ExistingImages = attraction.Images
                    .Select(i => new ExistingAttractionImageViewModel { Id = i.Id, ImageUrl = i.ImageUrl })
                    .ToList();
                model.CurrentMainImageUrl = attraction.ImageUrl;
                model.AdditionalImages = new List<IFormFile>();
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAvailability(int id)
        {
            var attraction = await _context.Attractions
                .FirstOrDefaultAsync(a => a.Id == id);

            if (attraction == null)
            {
                return NotFound();
            }

            attraction.IsAvailable = !attraction.IsAvailable;
            await _context.SaveChangesAsync();

            TempData["Success"] = attraction.IsAvailable
                ? $"{attraction.Name} is now available and can appear in search results."
                : $"{attraction.Name} has been taken offline. Existing order history has been preserved.";

            return RedirectToAction(nameof(Index));
        }

        private static AttractionFormViewModel ToFormViewModel(Attraction attraction)
        {
            return new AttractionFormViewModel
            {
                Id = attraction.Id,
                Name = attraction.Name,
                City = attraction.City,
                Description = attraction.Description,
                TicketPrice = attraction.TicketPrice,
                IsAvailable = attraction.IsAvailable,
                CurrentMainImageUrl = attraction.ImageUrl,
                ExistingImages = attraction.Images
                    .OrderBy(i => i.Id)
                    .Select(i => new ExistingAttractionImageViewModel
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl
                    })
                    .ToList()
            };
        }

        private static List<IFormFile> GetSelectedFiles(IEnumerable<IFormFile>? files)
        {
            return files?.Where(file => file != null && file.Length > 0).ToList()
                ?? new List<IFormFile>();
        }

        private void ValidateImage(IFormFile? file, string fieldName, bool required)
        {
            if (file == null || file.Length == 0)
            {
                if (required)
                {
                    ModelState.AddModelError(fieldName, "Please choose a main image.");
                }

                return;
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(fieldName, "Upload a JPG, JPEG, PNG, WEBP, or GIF image.");
            }

            if (file.Length > MaxFileSizeBytes)
            {
                ModelState.AddModelError(fieldName, "Each image must be 20 MB or smaller.");
            }
        }

        private void ValidateAdditionalImages(IReadOnlyList<IFormFile> images)
        {
            if (images.Count > MaxAdditionalImages)
            {
                ModelState.AddModelError(nameof(AttractionFormViewModel.AdditionalImages), $"Upload no more than {MaxAdditionalImages} additional images.");
                return;
            }

            for (var index = 0; index < images.Count; index++)
            {
                ValidateImage(images[index], nameof(AttractionFormViewModel.AdditionalImages), required: true);
            }
        }

        private async Task<string> SaveImageAsync(IFormFile image)
        {
            var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var uploadDirectory = GetAttractionUploadDirectory();
            Directory.CreateDirectory(uploadDirectory);

            var fullPath = Path.Combine(uploadDirectory, fileName);
            await using (var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await image.CopyToAsync(stream);
            }

            return "/uploads/attractions/" + fileName;
        }

        private string GetAttractionUploadDirectory()
        {
            var webRoot = _environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
            }

            return Path.Combine(webRoot, "uploads", "attractions");
        }

        private void DeleteUploadedImage(string? imageUrl)
        {
            const string uploadPrefix = "/uploads/attractions/";
            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith(uploadPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(imageUrl);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            var fullPath = Path.Combine(GetAttractionUploadDirectory(), fileName);
            try
            {
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not delete uploaded attraction image {ImageUrl}.", imageUrl);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Could not delete uploaded attraction image {ImageUrl}.", imageUrl);
            }
        }
    }
}
