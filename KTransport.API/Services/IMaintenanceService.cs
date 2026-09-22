using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IMaintenanceService
    {
        Task<List<VehicleMaintenanceDto>> GetMaintenanceRecordsAsync(long? vehicleId = null, MaintenanceType? maintenanceType = null);
        Task<VehicleMaintenanceDto?> GetMaintenanceByIdAsync(long id);
        Task<VehicleMaintenanceDto> CreateMaintenanceAsync(CreateMaintenanceRequest request);
        Task<bool> DeleteMaintenanceAsync(long id);
    }
}
