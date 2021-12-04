using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using WebServerImages.Data;
using WebServerImages.Models.Images;

namespace WebServerImages.Services
{
    public class FileImageService : IFileImageService
    {
        private const int ThumbnailWidth = 300;
        private const int FullscreenWidth = 1000;

        private readonly ApplicationDbContext _data;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public FileImageService(
            ApplicationDbContext data,
            IServiceScopeFactory serviceScopeFactory)
        {
            _data = data;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Process(IEnumerable<ImageInputModel> images)
        {
            // Instead of IServiceScopeFactory, we may use a concurrent dictionary to get the image data and then store everything in the database with a single transaction.

            var totalImages = await _data
                .ImageFiles
                .CountAsync();

            var tasks = images
                .Select(image => Task.Run(async () =>
                {
                    try
                    {
                        using var imageResult = await Image.LoadAsync(image.Content);

                        var id = Guid.NewGuid();
                        var path = $"/images/{totalImages % 1000}/";
                        var name = $"{id}.jpg";

                        var storagePath = Path.Combine(
                            Directory.GetCurrentDirectory(), $"wwwroot{path}".Replace("/", "\\"));

                        if (!Directory.Exists(storagePath))
                        {
                            Directory.CreateDirectory(storagePath);
                        }

                        await SaveImage(imageResult,
                            $"Original_{name}", storagePath, imageResult.Width);
                        await SaveImage(imageResult,
                            $"Fullscreen_{name}", storagePath, FullscreenWidth);
                        await SaveImage(imageResult,
                            $"Thumbnail_{name}", storagePath, ThumbnailWidth);

                        var database = _serviceScopeFactory
                            .CreateScope()
                            .ServiceProvider
                            .GetRequiredService<ApplicationDbContext>();

                        await database.ImageFiles.AddAsync(new ImageFile
                        {
                            Id = id,
                            Folder = path
                        });

                        await database.SaveChangesAsync();
                    }
                    catch
                    {
                        // Log information.
                    }
                }))
                .ToList();

            await Task.WhenAll(tasks);
        }

        public Task<List<string>> GetAllImages()
            => _data
                .ImageFiles
                .Select(i => i.Folder + "/Thumbnail_" + i.Id + ".jpg")
                .ToListAsync();

        private async Task SaveImage(Image image, string name, string path, int resizeWidth)
        {
            var width = image.Width;
            var height = image.Height;

            if (width > resizeWidth)
            {
                height = (int)((double)resizeWidth / width * height);
                width = resizeWidth;
            }

            image
                .Mutate(i => i
                    .Resize(new Size(width, height)));

            image.Metadata.ExifProfile = null;

            await image.SaveAsJpegAsync($"{path}/{name}", new JpegEncoder
            {
                Quality = 75
            });
        }
    }
}