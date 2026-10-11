using System;
using KTransport.API.Common;

namespace KTransport.API.Models
{
    public enum PaymentMode
    {
        Cash = 0,
        UPI = 1,
        NEFT = 2,
        Cheque = 3,
        BankTransfer = 4,
        Partial = 5
    }

    public class PaymentModeMaster : ITenantScopedEntity
    {
        public int Id { get; set; }

        public Guid TenantId { get; set; }

        public virtual Tenant? Tenant { get; set; }

        public string Code { get; set; } = null!; // CASH, UPI, NEFT, CHEQUE, BANK_TRANSFER, PARTIAL

        public string Name { get; set; } = null!; // Cash, UPI / QR Code, NEFT / RTGS, Cheque / DD, Bank Transfer, Partial Payment

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
