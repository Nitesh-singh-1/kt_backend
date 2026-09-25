using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IUserService
    {
        Task<List<SubUserDetailsDto>> GetTenantUsersAsync(Guid tenantId);
        Task<SubUserDetailsDto?> GetUserByIdAsync(Guid tenantId, int userId);
        Task<(bool Success, string Message, SubUserDetailsDto? Data)> CreateSubUserAsync(Guid tenantId, CreateSubUserRequest request);
        Task<(bool Success, string Message, SubUserDetailsDto? Data)> UpdateSubUserAsync(Guid tenantId, int userId, UpdateSubUserRequest request);
        Task<(bool Success, string Message)> UpdateUserPermissionsAsync(Guid tenantId, int userId, UpdateUserPermissionsRequest request);
        Task<(bool Success, string Message)> ToggleUserStatusAsync(Guid tenantId, int userId, bool isActive);
        Task<(bool Success, string Message)> AdminResetUserPasswordAsync(Guid tenantId, int userId, string newPassword);
        Task<(bool Success, string Message)> DeleteUserAsync(Guid tenantId, int userId);
    }
}
