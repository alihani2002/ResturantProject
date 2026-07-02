using System;
using System.Collections.Generic;
using Resturant.Core.Entities;

namespace Resturant.API.Models
{
    // --- Auth DTOs ---
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public int? DriverId { get; set; } // If the role is Driver, includes the Driver primary key ID
    }

    public class UserProfileResponse
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int? DriverId { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }

    // --- Driver DTOs ---
    public class DriverTripResponse
    {
        public int OrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal DeliveryFee { get; set; }
        public string Address { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    public class UpdateDriverStatusRequest
    {
        public DriverStatus Status { get; set; }
    }

    public class UpdateDeliveryStatusRequest
    {
        public OrderStatus Status { get; set; } // Delivered or FailedDelivery
        public string? FailureReason { get; set; }
    }

    public class DriverLocationRequest
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    // --- Waiter & User DTOs ---
    public class TableResponse
    {
        public int Id { get; set; }
        public int TableNumber { get; set; }
        public string Status { get; set; } = string.Empty; // Empty, Pending, Active
        public int? SessionId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class StartSessionRequest
    {
        public int TableNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ApiCreateOrderRequest
    {
        public int TableNumber { get; set; }
        public string? Note { get; set; }
        public List<ApiOrderItemRequest> OrderItems { get; set; } = new();
        public int? BranchId { get; set; }
        public string? PriceCategory { get; set; }
    }

    public class ApiOrderItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
        public List<int>? AddOnIds { get; set; }
        public decimal? Price { get; set; }
        public string? Size { get; set; }
    }

    public class OrderResponse
    {
        public int OrderId { get; set; }
        public int TableNumber { get; set; }
        public int? SessionId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public string? Note { get; set; }
        public List<OrderItemResponse> Items { get; set; } = new();
    }

    public class OrderItemResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public bool IsCancelled { get; set; }
        public string AddOns { get; set; } = string.Empty;
    }

    // --- Guest DTOs ---
    public class GuestSessionStartRequest
    {
        public int BranchId { get; set; }
        public int TableNumber { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class GuestSessionStartResponse
    {
        public string Token { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public int SessionId { get; set; }
        public int TableNumber { get; set; }
        public int BranchId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class GuestCreateOrderRequest
    {
        public string? Note { get; set; }
        public List<GuestOrderItemRequest> OrderItems { get; set; } = new();
    }

    public class GuestOrderItemRequest
    {
        public int MenuItemId { get; set; }
        public int Quantity { get; set; }
        public List<int>? AddOnIds { get; set; }
        public string? Size { get; set; }
    }
}
