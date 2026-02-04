using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Resturant.Core.Interfaces
{
    public interface ICloudinaryService
    {
        Task<string> UploadImageAsync(IFormFile file);
        Task<string> UploadVideoAsync(IFormFile file);
    }
}
