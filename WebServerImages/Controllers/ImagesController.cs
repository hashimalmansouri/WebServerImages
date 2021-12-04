using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using WebServerImages.Models.Images;
using WebServerImages.Services;

namespace WebServerImages.Controllers
{
    public class ImagesController : Controller
    {
        private readonly IImageService _imageService;

        public ImagesController(IImageService imageService)
            => _imageService = imageService;

        [HttpGet]
        public IActionResult Upload() => View();

        [HttpPost]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> Upload(IFormFile[] images)
        {
            if (images.Length > 10)
            {
                ModelState.AddModelError("images", "You cannot upload more than 10 images!");
                return View();
            }

            await _imageService.Process(images.Select(i => new ImageInputModel
            {
                Name = i.FileName,
                Type = i.ContentType,
                Content = i.OpenReadStream()
            }));

            return RedirectToAction(nameof(Done));
        }

        public async Task<IActionResult> All()
            => View(await _imageService.GetAllImages());

        public async Task<IActionResult> Thumbnail(string id)
            => ReturnImage(await _imageService.GetThumbnail(id));

        public async Task<IActionResult> Fullscreen(string id)
            => ReturnImage(await _imageService.GetFullscreen(id));

        private IActionResult ReturnImage(Stream image)
        {
            var headers = Response.GetTypedHeaders();

            headers.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(30)
            };

            headers.Expires = new DateTimeOffset(DateTime.UtcNow.AddDays(30));

            return File(image, "image/jpeg");
        }

        public IActionResult Done() => View();
    }
}