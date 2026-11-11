using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KTransport.API.Models.Gst;

namespace KTransport.API.Services.Gst;

public interface IGstRuleRepository
{
    Task<GstRule?> FindOneAsync(
        GstServiceType serviceType,
        bool providerIsGta,
        bool issuesConsignmentNote,
        bool providerGstRegistered,
        GtaTaxScheme gtaTaxScheme,
        RecipientCategory recipientCategory,
        PosRelation posRelation,
        DateOnly onDate);

    Task<GstRule?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<GstRule>> ListAsync(DateOnly? onDate, bool? active);
    Task<GstRule> AddAsync(GstRule rule, int? createdByUserId);
    Task<IReadOnlyList<GstRule>> AddManyAsync(IReadOnlyList<GstRule> rules, int? createdByUserId);
    Task<GstRule> UpdateWindowAsync(Guid id, DateOnly? effectiveTo, bool? active, int? updatedByUserId);
    Task<bool> IsReferencedByAuditAsync(Guid id);
}
