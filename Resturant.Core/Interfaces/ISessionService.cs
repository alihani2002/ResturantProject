/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Entities;
using System.Threading.Tasks;

namespace Resturant.Core.Interfaces
{
    public interface ISessionService
    {
        Task<TableSession> StartSessionAsync(int tableId, string customerName, string phoneNumber);
        Task<TableSession?> GetActiveSessionByTableAsync(int tableId);
        Task CloseSessionAsync(int sessionId);
    }
}
