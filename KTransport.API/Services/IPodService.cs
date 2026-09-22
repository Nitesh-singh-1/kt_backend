using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IPodService
    {
        Task<List<PodRecordDto>> GetPodsAsync(PodStatus? status = null, string? search = null);
        Task<PodRecordDto?> GetPodByShipmentIdAsync(long shipmentId);
        Task<PodRecordDto> UploadPodAsync(UploadPodRequest request, int? userId = null);
        Task<PodRecordDto?> VerifyPodAsync(long id, VerifyPodRequest request, int? userId = null);
        Task<bool> DeletePodAsync(long id);
    }
}
