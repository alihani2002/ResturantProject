using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Resturant.Core.DTOs.Reports;

namespace Resturant.Core.Interfaces
{
    public interface IAnalyticsService
    {
        // Keep original methods for backward compatibility
        Task<decimal> GetTotalSalesAsync(DateTime start, DateTime end);
        Task<Dictionary<string, int>> GetMostOrderedProductsAsync(int topCount);
        Task<decimal> GetAverageOrderValueAsync(DateTime start, DateTime end);

        // Advanced Restaurant Enterprise ERP Reporting APIs
        Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? branchId = null);
        Task<SalesAnalyticsDto> GetSalesReportsAsync(ReportFilterParams filters);
        Task<OrderAnalyticsDto> GetOrderReportsAsync(ReportFilterParams filters);
        Task<ProductAnalyticsDto> GetProductReportsAsync(ReportFilterParams filters);
        Task<CustomerAnalyticsDto> GetCustomerReportsAsync(ReportFilterParams filters);
        Task<StaffAnalyticsDto> GetStaffReportsAsync(ReportFilterParams filters);
        Task<KitchenAnalyticsDto> GetKitchenReportsAsync(ReportFilterParams filters);
        Task<FinancialAnalyticsDto> GetFinancialReportsAsync(ReportFilterParams filters);
        Task<InventoryAnalyticsDto> GetInventoryReportsAsync(ReportFilterParams filters);
    }
}
