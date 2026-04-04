using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace VinhKhanhApi.Controllers
{
    [Route("api/uploads")]
    [ApiController]
    [Authorize(Roles = "Admin,Owner")]
    public class UploadsController : ControllerBase
    {
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

        [HttpPost("menu")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadMenuAsync(IFormFile file)
        {
            return await SaveFileAsync(file, "menus");
        }

        [HttpPost("poi")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadPoiAsync(IFormFile file)
        {
            return await SaveFileAsync(file, "pois");
        }

        private async Task<IActionResult> SaveFileAsync(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("Vui lòng ch?n ?nh h?p l?.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
            {
                return BadRequest("Ch? h? tr? file ?nh JPG, PNG, WEBP.");
            }

            var uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var relativeUrl = $"/uploads/{folder}/{fileName}";
            var absoluteUrl = $"{Request.Scheme}://{Request.Host}{relativeUrl}";

            return Ok(new
            {
                url = absoluteUrl,
                relativeUrl
            });
        }
    }
}
