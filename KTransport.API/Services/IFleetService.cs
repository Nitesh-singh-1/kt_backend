using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;

namespace KTransport.API.Services
{
    public interface IFleetService
    {
        // Vehicles
        Task<List<VehicleDto>> GetVehiclesAsync(string? search = null);
        Task<List<VehicleLookupDto>> GetVehicleLookupAsync(string? query = null);
        Task<VehicleDto?> GetVehicleByIdAsync(long id);
        Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request);
        Task<bool> DeleteVehicleAsync(long id);

        // Drivers
        Task<List<DriverDto>> GetDriversAsync(string? search = null);
        Task<List<DriverLookupDto>> GetDriverLookupAsync(string? query = null);
        Task<DriverDto?> GetDriverByIdAsync(long id);
        Task<DriverDto> CreateDriverAsync(CreateDriverRequest request);
        Task<bool> DeleteDriverAsync(long id);

        // Locations / Stations
        Task<List<LocationDto>> GetLocationsAsync(string? search = null);
        Task<List<LocationLookupDto>> GetLocationLookupAsync(string? query = null);
        Task<LocationDto?> GetLocationByIdAsync(long id);
        Task<LocationDto> CreateLocationAsync(CreateLocationRequest request);
        Task<bool> DeleteLocationAsync(long id);
    }
}
