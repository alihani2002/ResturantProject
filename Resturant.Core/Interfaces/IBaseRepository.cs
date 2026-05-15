using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Query;
using Resturant.Core.Common;

namespace Resturant.Core.Interfaces
{
    public interface IBaseRepository<T> where T : class
    {
        Task<T> AddAsync(T entity);
        Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities);
        Task<bool> IsExistsAsync(Expression<Func<T, bool>> predicate);
        Task<int> CountAsync();
        Task<int> CountAsync(Expression<Func<T, bool>> predicate);
        Task<T?> GetByIdAsync(int id);
        Task<IEnumerable<T>> ListAsync(Expression<Func<T, bool>> predicate);
        Task<T?> GetFirstOrDefaultAsync(Expression<Func<T, bool>> predicate);
        Task<List<T>> GetAllWithIncludesAsync(params Expression<Func<T, object>>[] includes);
        IEnumerable<T> GetQueryable(bool withNoTracking = true);
        IQueryable<T> GetQueryable();
        PaginatedList<T> GetPaginatedList(IQueryable<T> query, int pageNumber, int pageSize);
        T? GetById(int id);
        T? Find(Expression<Func<T, bool>> predicate);
        T? Find(Expression<Func<T, bool>> predicate, string[]? includes = null);
        T? Find(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null);
        IEnumerable<T> FindAll(Expression<Func<T, bool>> predicate, Expression<Func<T, object>>? orderBy = null, string? orderByDirection = OrderBy.Ascending);
        IEnumerable<T> FindAll(Expression<Func<T, bool>> predicate, int? skip = null, int? take = null, Expression<Func<T, object>>? orderBy = null, string? orderByDirection = OrderBy.Ascending);
        IEnumerable<T> FindAll(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IIncludableQueryable<T, object>>? include = null, Expression<Func<T, object>>? orderBy = null, string? orderByDirection = OrderBy.Ascending);
        IQueryable<T> FindAllAsQueryable(Expression<Func<T, bool>> predicate, Expression<Func<T, object>>? orderBy = null, string? orderByDirection = OrderBy.Ascending);
        T Add(T entity);
        IEnumerable<T> AddRange(IEnumerable<T> entities);
        void Update(T entity);
        void Remove(T entity);
        void RemoveRange(IEnumerable<T> entities);
        void DeleteBulk(Expression<Func<T, bool>> predicate);
        bool IsExists(Expression<Func<T, bool>> predicate);
        int Count();
        int Count(Expression<Func<T, bool>> predicate);
        int Max(Expression<Func<T, bool>> predicate, Expression<Func<T, int>> field);
    }
}
