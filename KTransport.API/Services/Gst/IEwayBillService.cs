using System;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

public interface IEwayBillService
{
    Task<EwayBillDto> FetchAsync(string ewbNumber);
    Task<EwayBillDto> GenerateAsync(GenerateEwbRequestDto request, int? userId);
    Task<EwayBillDto> UpdateVehicleAsync(UpdateVehicleRequestDto request);
    Task<EwayBillDto> CancelAsync(CancelEwbRequestDto request);
    Task<EwayBillDto> ExtendValidityAsync(ExtendValidityRequestDto request);
    Task<EwayBillDto?> GetFromCacheAsync(string ewbNumber);
}
