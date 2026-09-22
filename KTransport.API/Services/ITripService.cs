using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface ITripService
    {
        Task<List<TripDto>> GetTripsAsync(string? search = null, TripStatus? status = null);
        Task<List<TripLookupDto>> GetTripLookupAsync(string? query = null);
        Task<TripDto?> GetTripByIdAsync(long id);
        Task<TripDto> CreateTripAsync(CreateTripRequest request, int? userId = null);
        Task<TripDto?> UpdateTripAsync(long id, UpdateTripRequest request, int? userId = null);
        Task<TripDto?> LoadShipmentsAsync(long tripId, LoadShipmentsRequest request, int? userId = null);
        Task<TripDto?> RemoveShipmentAsync(long tripId, long shipmentId, int? userId = null);
        Task<TripDto?> DispatchTripAsync(long tripId, int? userId = null);
        Task<TripDto?> ArriveTripAsync(long tripId, int? userId = null);
        Task<TripDto?> CompleteTripAsync(long tripId, decimal endOdometer, int? userId = null);
        Task<TripDto?> CancelTripAsync(long tripId, string? reason = null, int? userId = null);
        Task<TripExpenseDto> AddTripExpenseAsync(long tripId, AddTripExpenseRequest request, int? userId = null);
        Task<bool> DeleteTripExpenseAsync(long tripId, long expenseId);
    }
}
