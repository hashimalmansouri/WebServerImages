using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WebServerImages.Models.Images;

namespace WebServerImages.Services
{
    public interface IImageService
    {
        Task Process(IEnumerable<ImageInputModel> images);

        Task<Stream> GetThumbnail(string id);

        Task<Stream> GetFullscreen(string id);

        Task<List<string>> GetAllImages();
    }
}