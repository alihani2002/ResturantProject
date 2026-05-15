/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using System.Threading.Tasks;

namespace Resturant.Services.Services
{
    public class TableService : ITableService
    {
        private readonly IUnitOfWork _unitOfWork;

        public TableService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<RestaurantTable> CreateTableAsync(int tableNumber)
        {
            var table = new RestaurantTable { TableNumber = tableNumber };
            await _unitOfWork.Repository<RestaurantTable>().AddAsync(table);
            await _unitOfWork.CompleteAsync();
            
            // Generate QR code after creation
            table.QrCodeImageUrl = await GenerateQrCodeAsync(table.Id);
            _unitOfWork.Repository<RestaurantTable>().Update(table);
            await _unitOfWork.CompleteAsync();
            
            return table;
        }

        public async Task<string> GenerateQrCodeAsync(int tableId)
        {
            // In a real implementation, use QRCoder library
            // For now, return a placeholder URL that encodes the table ID
            return $"/api/tables/qr/{tableId}";
        }

        public async Task<RestaurantTable?> GetTableByNumberAsync(int tableNumber)
        {
            return await _unitOfWork.Repository<RestaurantTable>()
                .GetFirstOrDefaultAsync(t => t.TableNumber == tableNumber);
        }
    }
}
