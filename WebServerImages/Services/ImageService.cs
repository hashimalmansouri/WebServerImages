using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using WebServerImages.Data;
using WebServerImages.Models.Images;

namespace WebServerImages.Services
{
    public class ImageService : IImageService
    {
        private const int ThumbnailWidth = 300;
        private const int FullscreenWidth = 1000;

        private readonly ApplicationDbContext _data;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ImageService(
            ApplicationDbContext data,
            IServiceScopeFactory serviceScopeFactory)
        {
            _data = data;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Process(IEnumerable<ImageInputModel> images)
        {
            // Instead of IServiceScopeFactory, we may use a concurrent dictionary to get the image data and then store everything in the database with a single transaction.

            var tasks = images
                .Select(image => Task.Run(async () =>
                {
                    try
                    {
                        using var imageResult = await Image.LoadAsync(image.Content);

                        var original = await SaveImage(imageResult, imageResult.Width);
                        var fullscreen = await SaveImage(imageResult, FullscreenWidth);
                        var thumbnail = await SaveImage(imageResult, ThumbnailWidth);

                        var database = _serviceScopeFactory
                            .CreateScope()
                            .ServiceProvider
                            .GetRequiredService<ApplicationDbContext>();

                        await database.ImageData.AddAsync(new ImageData
                        {
                            OriginalFileName = image.Name,
                            OriginalType = image.Type,
                            OriginalContent = original,
                            ThumbnailContent = thumbnail,
                            FullscreenContent = fullscreen
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

        public Task<Stream> GetThumbnail(string id)
            => GetImageData(id, "Thumbnail");

        public Task<Stream> GetFullscreen(string id)
            => GetImageData(id, "Fullscreen");

        public Task<List<string>> GetAllImages()
            => _data
                .ImageData
                .Select(i => i.Id.ToString())
                .ToListAsync();

        private async Task<byte[]> SaveImage(Image image, int resizeWidth)
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

            var memoryStream = new MemoryStream();

            await image.SaveAsJpegAsync(memoryStream, new JpegEncoder
            {
                Quality = 75
            });

            return memoryStream.ToArray();
        }

        private async Task<Stream> GetImageData(string id, string size)
        {
            var database = _data.Database;

            var dbConnection = (SqlConnection)database.GetDbConnection();

            var command = new SqlCommand(
                $"SELECT {size}Content FROM ImageData WHERE Id = @id;",
                dbConnection);

            command.Parameters.Add(new SqlParameter("@id", id));

            dbConnection.Open();

            var reader = await command.ExecuteReaderAsync();

            Stream result = null;

            if (reader.HasRows)
            {
                while (reader.Read()) result = reader.GetStream(0);
            }

            await reader.CloseAsync();

            return result;
        }
    }
}