using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IShipmentService
    {
        Task<ShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, int userId);
        Task<ShipmentResponse> UpdateShipmentAsync(long id, UpdateShipmentRequest request, int userId);
        Task<ShipmentResponse> UpdateShipmentStatusAsync(long id, UpdateShipmentStatusRequest request, int userId);
        Task<ShipmentResponse> GetShipmentByIdAsync(long id);
        Task<ShipmentResponse> GetShipmentByNoAsync(string shipmentNo);
        Task<ShipmentListResponse> GetAllShipmentsAsync(ShipmentStatus? status = null, TaxTreatment? taxTreatment = null, string? search = null, int page = 1, int pageSize = 50);
        Task<bool> DeleteShipmentAsync(long id, int userId);
    }
}
