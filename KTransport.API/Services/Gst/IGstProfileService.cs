using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

public interface IGstProfileService
{
    Task<CompanyGstProfileDto?> GetActiveCompanyAsync(DateOnly? onDate);
    Task<IReadOnlyList<CompanyGstProfileDto>> ListCompanyHistoryAsync();
    Task<CompanyGstProfileDto> UpsertCompanyAsync(CompanyGstProfileWriteDto write);

    Task<PartyGstProfileDto?> GetActivePartyAsync(int partyId, DateOnly? onDate);
    Task<IReadOnlyList<PartyGstProfileDto>> ListPartyHistoryAsync(int partyId);
    Task<PartyGstProfileDto> UpsertPartyAsync(int partyId, PartyGstProfileWriteDto write);
}
