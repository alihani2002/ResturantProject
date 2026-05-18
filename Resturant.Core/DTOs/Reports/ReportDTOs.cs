using System;
using System.Collections.Generic;

namespace Resturant.Core.DTOs.Reports
{
    // Common Filter parameters
    public class ReportFilterParams
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? BranchId { get; set; } // For multi-branch future scalability
        public int? MenuItemId { get; set; }
        public int? MenuCategoryId { get; set; }
        public string? WaiterId { get; set; }
        public string? PaymentMethod { get; set; }
        public int? TableNumber { get; set; }
    }

    // 1. SALES DTOs
    public class SalesAnalyticsDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal NetRevenue { get; set; }
        public decimal GrossRevenue { get; set; }
        public decimal TotalTaxes { get; set; }
        public decimal TotalServiceCharges { get; set; }
        public decimal TotalDiscounts { get; set; }
        public decimal TotalRefunds { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal RevenueGrowthPercentage { get; set; }
        public List<SalesOverTimeDto> SalesTrend { get; set; } = new List<SalesOverTimeDto>();
        public List<HourlySalesDto> HourlySales { get; set; } = new List<HourlySalesDto>();
        public List<SalesByDimensionDto> SalesByDayOfWeek { get; set; } = new List<SalesByDimensionDto>();
        public List<SalesByDimensionDto> SalesByMonth { get; set; } = new List<SalesByDimensionDto>();
        public List<SalesByDimensionDto> SalesByBranch { get; set; } = new List<SalesByDimensionDto>();
        public List<SalesByDimensionDto> SalesByTable { get; set; } = new List<SalesByDimensionDto>();
        public List<SalesByDimensionDto> SalesByWaiter { get; set; } = new List<SalesByDimensionDto>();
        public List<SalesByDimensionDto> SalesByPaymentMethod { get; set; } = new List<SalesByDimensionDto>();
    }

    public class SalesOverTimeDto
    {
        public string DateLabel { get; set; } // e.g. "2026-05-18" or "W20" or "May 2026"
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
    }

    public class HourlySalesDto
    {
        public int Hour { get; set; } // 0 - 23
        public string HourLabel { get; set; } // e.g. "12:00 PM"
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public string ActivityLevel { get; set; } // "Peak", "Normal", "Slow"
    }

    public class SalesByDimensionDto
    {
        public string DimensionValue { get; set; } // Table name, Waiter Name, Payment Method, Day, etc.
        public decimal Revenue { get; set; }
        public int OrderCount { get; set; }
        public double PercentageOfTotal { get; set; }
    }

    // 2. ORDER DTOs
    public class OrderAnalyticsDto
    {
        public int TotalOrders { get; set; }
        public int ActiveOrders { get; set; }
        public int CancelledOrders { get; set; }
        public int PaidOrders { get; set; }
        public int PendingOrders { get; set; }
        public int PreparingOrders { get; set; }
        public int ReadyOrders { get; set; }
        public int ServedOrders { get; set; }
        public double AveragePreparationTimeMinutes { get; set; } // In minutes
        public double AverageServingTimeMinutes { get; set; } // In minutes
        public double AverageTableTurnoverMinutes { get; set; }
        public List<TableTurnoverDto> MostBusyTables { get; set; } = new List<TableTurnoverDto>();
        public List<SalesByDimensionDto> OrdersByCategory { get; set; } = new List<SalesByDimensionDto>();
        public List<SalesByDimensionDto> OrdersByProduct { get; set; } = new List<SalesByDimensionDto>();
        public List<SalesByDimensionDto> OrdersByWaiter { get; set; } = new List<SalesByDimensionDto>();
    }

    public class TableTurnoverDto
    {
        public int TableNumber { get; set; }
        public int TotalSessions { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public double AverageSessionDurationMinutes { get; set; }
    }

    // 3. PRODUCT DTOs
    public class ProductAnalyticsDto
    {
        public List<ProductPerformanceDto> BestSellers { get; set; } = new List<ProductPerformanceDto>();
        public List<ProductPerformanceDto> LeastSellers { get; set; } = new List<ProductPerformanceDto>();
        public List<ProductPerformanceDto> TrendingProducts { get; set; } = new List<ProductPerformanceDto>();
        public List<ProductPerformanceDto> MostProfitable { get; set; } = new List<ProductPerformanceDto>();
        public List<CategoryPerformanceDto> CategoryPerformance { get; set; } = new List<CategoryPerformanceDto>();
        public List<AddOnPerformanceDto> AddOnPerformance { get; set; } = new List<AddOnPerformanceDto>();
        public double UpsellPerformanceRate { get; set; } // % of orders with add-ons
        public double RecommendationConversionRate { get; set; } // % of accepted recommendations
    }

    public class ProductPerformanceDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public int SalesCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal Profit { get; set; }
        public double PopularityScore { get; set; } // Calculated index 0-100
        public string Badge { get; set; } // "Best Seller", "Trending", "Recommended", "Regular"
    }

    public class CategoryPerformanceDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int ItemCount { get; set; }
        public int TotalSalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public class AddOnPerformanceDto
    {
        public int AddOnId { get; set; }
        public string AddOnName { get; set; }
        public int TimesAdded { get; set; }
        public decimal Revenue { get; set; }
    }

    // 4. CUSTOMER DTOs
    public class CustomerAnalyticsDto
    {
        public int NewCustomersCount { get; set; }
        public int ReturningCustomersCount { get; set; }
        public int VIPCustomersCount { get; set; }
        public double CustomerRetentionRate { get; set; }
        public double AverageVisitFrequencyDays { get; set; }
        public decimal AverageCustomerSpend { get; set; }
        public int LoyaltyProgramSubscribers { get; set; }
        public int CouponUsageCount { get; set; }
        public List<CustomerSpendDto> VIPList { get; set; } = new List<CustomerSpendDto>();
        public List<CustomerSegmentDto> Segments { get; set; } = new List<CustomerSegmentDto>();
    }

    public class CustomerSpendDto
    {
        public string PhoneNumber { get; set; }
        public string CustomerName { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal CustomerLifetimeValue { get; set; }
        public string FavoriteProduct { get; set; }
        public DateTime LastVisit { get; set; }
    }

    public class CustomerSegmentDto
    {
        public string SegmentName { get; set; } // "VIP", "Frequent", "Occasional", "Churn Risk", "New"
        public int CustomerCount { get; set; }
        public decimal TotalRevenueContribution { get; set; }
        public double Percentage { get; set; }
    }

    // 5. WAITER & STAFF DTOs
    public class StaffAnalyticsDto
    {
        public List<WaiterPerformanceDto> WaiterRankings { get; set; } = new List<WaiterPerformanceDto>();
        public List<ShiftPerformanceDto> Shifts { get; set; } = new List<ShiftPerformanceDto>();
    }

    public class WaiterPerformanceDto
    {
        public string WaiterId { get; set; }
        public string WaiterName { get; set; }
        public decimal TotalRevenue { get; set; }
        public int OrdersHandled { get; set; }
        public double AverageServiceTimeMinutes { get; set; }
        public int TablesManagedCount { get; set; }
        public int ActiveSessionsCount { get; set; }
        public double CustomerRating { get; set; } // 1.0 - 5.0
        public string PerformanceBadge { get; set; } // "Fastest", "Highest Sales", "Star Performer", "Standard"
    }

    public class ShiftPerformanceDto
    {
        public string ShiftName { get; set; } // "Morning", "Evening", "Night"
        public string Hours { get; set; }
        public decimal TotalRevenue { get; set; }
        public int OrdersHandled { get; set; }
        public int ActiveStaff { get; set; }
    }

    // 6. KITCHEN DTOs
    public class KitchenAnalyticsDto
    {
        public double KitchenEfficiencyScore { get; set; } // Index 0-100
        public double AveragePreparationTimeMinutes { get; set; }
        public int DelayedOrdersCount { get; set; }
        public double DelayedPercentage { get; set; }
        public List<KitchenItemPrepDto> FastestItems { get; set; } = new List<KitchenItemPrepDto>();
        public List<KitchenItemPrepDto> SlowestItems { get; set; } = new List<KitchenItemPrepDto>();
        public List<KitchenLoadHourDto> KitchenLoadByHour { get; set; } = new List<KitchenLoadHourDto>();
    }

    public class KitchenItemPrepDto
    {
        public string ItemName { get; set; }
        public double AveragePrepTimeMinutes { get; set; }
        public int TimesPrepared { get; set; }
    }

    public class KitchenLoadHourDto
    {
        public int Hour { get; set; }
        public string HourLabel { get; set; }
        public int ActiveOrders { get; set; }
        public int DelayedOrders { get; set; }
        public double LoadPercentage { get; set; } // 0-100 based on capacity
    }

    // 7. FINANCIAL DTOs
    public class FinancialAnalyticsDto
    {
        public decimal NetProfit { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal OperationalCosts { get; set; }
        public decimal TotalTaxAmount { get; set; }
        public decimal TotalVatAmount { get; set; }
        public decimal FoodCosts { get; set; }
        public decimal LaborCosts { get; set; }
        public decimal ProfitMarginPercentage { get; set; }
        public List<ExpenseBreakdownDto> ExpensesBreakdown { get; set; } = new List<ExpenseBreakdownDto>();
        public List<DailyClosingReportDto> ClosingReports { get; set; } = new List<DailyClosingReportDto>();
        public List<PaymentReconciliationDto> PaymentReconciliations { get; set; } = new List<PaymentReconciliationDto>();
    }

    public class ExpenseBreakdownDto
    {
        public string Category { get; set; } // "Ingredients", "Labor", "Utilities", "Rent", "Marketing", "Waste", "Other"
        public decimal Amount { get; set; }
        public double PercentageOfExpenses { get; set; }
    }

    public class DailyClosingReportDto
    {
        public DateTime Date { get; set; }
        public string ClosedBy { get; set; }
        public decimal ExpectedCash { get; set; }
        public decimal ActualCash { get; set; }
        public decimal CashDiscrepancy { get; set; }
        public decimal CardRevenue { get; set; }
        public decimal OnlineRevenue { get; set; }
        public decimal TotalClosedRevenue { get; set; }
        public int ShiftCount { get; set; }
        public bool IsReconciled { get; set; }
    }

    public class PaymentReconciliationDto
    {
        public string PaymentMethod { get; set; } // "Cash", "Visa", "MasterCard", "ApplePay", "InstaPay", "Online"
        public int SystemTransactionCount { get; set; }
        public decimal SystemRevenue { get; set; }
        public int PhysicalReceiptsCount { get; set; }
        public decimal PhysicalRevenue { get; set; }
        public decimal Discrepancy { get; set; }
    }

    // 8. INVENTORY DTOs
    public class InventoryAnalyticsDto
    {
        public decimal TotalInventoryValuation { get; set; }
        public decimal TotalPurchasesAmount { get; set; }
        public decimal TotalWasteAmount { get; set; }
        public double AverageFoodCostPercentage { get; set; }
        public List<LowStockItemDto> LowStockAlerts { get; set; } = new List<LowStockItemDto>();
        public List<IngredientUsageDto> TopIngredientsConsumed { get; set; } = new List<IngredientUsageDto>();
        public List<RecipeCostAnalysisDto> RecipeCosts { get; set; } = new List<RecipeCostAnalysisDto>();
        public List<WasteLogDto> WasteLogs { get; set; } = new List<WasteLogDto>();
        public List<SupplierPurchaseDto> SupplierPurchases { get; set; } = new List<SupplierPurchaseDto>();
    }

    public class LowStockItemDto
    {
        public int IngredientId { get; set; }
        public string IngredientName { get; set; }
        public double CurrentStock { get; set; }
        public double ReorderLevel { get; set; }
        public string Unit { get; set; } // "Kg", "Ltr", "Pcs"
        public string SupplierName { get; set; }
        public string StockStatus { get; set; } // "Critical", "Low", "Reorder"
    }

    public class IngredientUsageDto
    {
        public string IngredientName { get; set; }
        public double QuantityUsed { get; set; }
        public string Unit { get; set; }
        public decimal EstimatedCost { get; set; }
    }

    public class RecipeCostAnalysisDto
    {
        public int MenuItemId { get; set; }
        public string MenuItemName { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal RawIngredientCost { get; set; }
        public decimal MarginAmount => SellingPrice - RawIngredientCost;
        public double FoodCostPercentage => SellingPrice > 0 ? (double)(RawIngredientCost / SellingPrice) * 100.0 : 0.0;
        public string ProfitabilityCategory { get; set; } // "High Margin", "Standard", "Low Margin", "Loss Leader"
    }

    public class WasteLogDto
    {
        public int LogId { get; set; }
        public string IngredientName { get; set; }
        public double QuantityWasted { get; set; }
        public string Unit { get; set; }
        public decimal Cost { get; set; }
        public string Reason { get; set; } // "Expired", "Spoiled", "Burnt/CookError", "CustomerReturn"
        public DateTime WasteDate { get; set; }
    }

    public class SupplierPurchaseDto
    {
        public string SupplierName { get; set; }
        public int PurchaseOrdersCount { get; set; }
        public decimal TotalPurchaseAmount { get; set; }
        public double LeadTimeDays { get; set; }
        public double QualityRating { get; set; } // 1.0 - 5.0
    }

    // 9. DASHBOARD SUMMARY DTO
    public class DashboardSummaryDto
    {
        public decimal TotalRevenueToday { get; set; }
        public int ActiveTablesCount { get; set; }
        public int PendingOrdersCount { get; set; }
        public int CompletedOrdersToday { get; set; }
        
        // Trends comparing with yesterday
        public decimal RevenueGrowthRate { get; set; } // +5.4% etc.
        public decimal ActiveSessionsTodayCount { get; set; }
        
        // Quick Live charts data
        public List<decimal> LiveSalesIntervals { get; set; } = new List<decimal>();
        public List<string> LiveTimeLabels { get; set; } = new List<string>();
        
        // Notifications
        public List<LiveNotificationDto> RecentNotifications { get; set; } = new List<LiveNotificationDto>();
    }

    public class LiveNotificationDto
    {
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; } // "info", "warning", "success", "error"
        public DateTime Timestamp { get; set; }
    }
}
