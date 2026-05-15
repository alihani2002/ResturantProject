/* 
 * NOTE: didn't create migration or database
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Resturant.Core.Interfaces
{
    public interface IAnalyticsService
    {
        Task<decimal> GetTotalSalesAsync(DateTime start, DateTime end);
        Task<Dictionary<string, int>> GetMostOrderedProductsAsync(int topCount);
        Task<decimal> GetAverageOrderValueAsync(DateTime start, DateTime end);
        // Add more analytics methods as per requirements
    }
}
