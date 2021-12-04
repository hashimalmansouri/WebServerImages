namespace WebServerImages.Controllers
{
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Models.Images;
    using Services;

    public class FileImagesController : Controller
    {
        private readonly IFileImageService _imageService;

        public FileImagesController(IFileImageService imageService)
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

        public IActionResult Done() => View();
    }
}