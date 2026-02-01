using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Resturant.Core.Common;

namespace Resturant.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IBaseRepository<T> Repository<T>() where T : BaseEntity;
        Task<int> CompleteAsync();
    }
}
