using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IMoneyReceiptService
    {
        Task<List<MoneyReceiptDto>> GetReceiptsAsync(MoneyReceiptFilterRequest filter);
        Task<MoneyReceiptDto?> GetReceiptByIdAsync(long id);
        Task<MoneyReceiptDto?> GetReceiptByShipmentIdAsync(long shipmentId);
    }
}
