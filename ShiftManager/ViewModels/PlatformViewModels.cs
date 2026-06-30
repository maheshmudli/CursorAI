using System;
using System.Collections.Generic;
using ShiftManager.Models;

namespace ShiftManager.ViewModels
{
    public class PlatformDashboardViewModel
    {
        public int TotalRegistrations { get; set; }
        public int ActiveTenants { get; set; }
        public int TotalUsers { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<RegistrationSummary> RecentRegistrations { get; set; } = new();
    }

    public class RegistrationSummary
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? PaymentIntentId { get; set; }
        public int? TenantId { get; set; }
    }

    public class TenantDetailViewModel
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public int SeatAllowance { get; set; }
        public int UserCount { get; set; }
        public List<TenantUserSummary> Users { get; set; } = new();
        public List<PaymentSummary> Payments { get; set; } = new();
        public List<SeatPurchaseSummary> SeatPurchases { get; set; } = new();
        public ProvisioningSummary? Provisioning { get; set; }
    }

    public class TenantUserSummary
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime? LastLogin { get; set; }
    }

    public class PaymentSummary
    {
        public int Id { get; set; }
        public string StripePaymentIntentId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public class SeatPurchaseSummary
    {
        public int Id { get; set; }
        public string StripePaymentIntentId { get; set; } = string.Empty;
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int SeatsAdded { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ProvisioningSummary
    {
        public int Id { get; set; }
        public DateTime ProvisionedAt { get; set; }
        public string Route { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class PlatformTenantsListViewModel
    {
        public List<TenantListItem> Tenants { get; set; } = new();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    public class TenantListItem
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public int SeatAllowance { get; set; }
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ValidationPaymentStatus { get; set; } = string.Empty;
    }

    public class PlatformRegistrationsViewModel
    {
        public List<RegistrationAttemptItem> Attempts { get; set; } = new();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    public class RegistrationAttemptItem
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? StripePaymentIntentId { get; set; }
        public int? TenantId { get; set; }
        public string? FailureReason { get; set; }
    }

    public class PlatformAccessLogsViewModel
    {
        public List<AccessLogItem> Logs { get; set; } = new();
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }

    public class AccessLogItem
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime LastLoginAt { get; set; }
    }
}
