using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public class TripExpense : ITenantScopedEntity
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public long TripId { get; set; }

        public virtual Trip? Trip { get; set; }

        public TripExpenseType ExpenseType { get; set; } = TripExpenseType.Fuel;

        public decimal Amount { get; set; } = 0;

        public string? ReceiptNo { get; set; }

        public string? PaymentMode { get; set; } // Cash, Card, Fastag, UPI

        public string? PaidTo { get; set; }

        public string? Remarks { get; set; }

        public DateOnly ExpenseDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
