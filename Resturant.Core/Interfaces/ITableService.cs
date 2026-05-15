/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Entities;
using System.Threading.Tasks;

namespace Resturant.Core.Interfaces
{
    public interface ITableService
    {
        Task<RestaurantTable> CreateTableAsync(int tableNumber);
        Task<string> GenerateQrCodeAsync(int tableId);
        Task<RestaurantTable?> GetTableByNumberAsync(int tableNumber);
    }
}
