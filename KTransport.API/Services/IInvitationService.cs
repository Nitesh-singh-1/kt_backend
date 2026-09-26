using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    public interface IInvitationService
    {
        Task<(bool Success, string Message, InviteDto? Data)> CreateInviteAsync(Guid tenantId, int? invitedByUserId, CreateInviteRequest request);
        Task<List<InviteDto>> GetInvitesAsync(Guid tenantId);
        Task<(bool Success, string Message)> RevokeInviteAsync(Guid tenantId, long inviteId);
        Task<InviteInfoDto> GetInviteInfoByTokenAsync(string token);
        Task<AuthResponse> AcceptInviteAsync(AcceptInviteRequest request);
    }
}
