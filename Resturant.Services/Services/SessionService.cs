/* 
 * NOTE: didn't create migration or database
 */
using Resturant.Core.Entities;
using Resturant.Core.Interfaces;
using System;
using System.Threading.Tasks;

namespace Resturant.Services.Services
{
    public class SessionService : ISessionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SessionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<TableSession> StartSessionAsync(int tableId, string customerName, string phoneNumber)
        {
            var existingSession = await GetActiveSessionByTableAsync(tableId);
            if (existingSession != null) return existingSession;

            var session = new TableSession
            {
                TableId = tableId,
                CustomerName = customerName,
                PhoneNumber = phoneNumber,
                StartTime = DateTime.Now,
                IsActive = true
            };

            await _unitOfWork.Repository<TableSession>().AddAsync(session);
            await _unitOfWork.CompleteAsync();
            return session;
        }

        public async Task<TableSession?> GetActiveSessionByTableAsync(int tableId)
        {
            return await _unitOfWork.Repository<TableSession>()
                .GetFirstOrDefaultAsync(s => s.TableId == tableId && s.IsActive);
        }

        public async Task CloseSessionAsync(int sessionId)
        {
            var session = await _unitOfWork.Repository<TableSession>().GetByIdAsync(sessionId);
            if (session != null)
            {
                session.IsActive = false;
                session.EndTime = DateTime.Now;
                _unitOfWork.Repository<TableSession>().Update(session);
                await _unitOfWork.CompleteAsync();
            }
        }
    }
}
