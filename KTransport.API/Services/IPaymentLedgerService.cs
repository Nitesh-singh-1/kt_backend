using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IPaymentLedgerService
    {
        /// <summary>All money-in events for a bilty, newest first.</summary>
        Task<List<ShipmentPaymentDto>> GetByShipmentAsync(long shipmentId);

        /// <summary>
        /// Reverse one payment event (full restore: cash and any discount). Recomputes the
        /// bilty's paid/due/settled from the remaining active ledger rows, reverses the linked
        /// accounting voucher, and records who/when/why on the row. Bill Book payments are not
        /// reversible here (reverse via the bill).
        /// </summary>
        Task<(bool Success, string Message)> ReverseAsync(long paymentId, string? reason, int userId);
    }
}
