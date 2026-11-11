using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.Models.Gst;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services.Gst;

public class GstRuleRepository : IGstRuleRepository
{
    private readonly KTransportDbContext _db;

    public GstRuleRepository(KTransportDbContext db)
    {
        _db = db;
    }

    public async Task<GstRule?> FindOneAsync(
        GstServiceType serviceType,
        bool providerIsGta,
        bool issuesConsignmentNote,
        bool providerGstRegistered,
        GtaTaxScheme gtaTaxScheme,
        RecipientCategory recipientCategory,
        PosRelation posRelation,
        DateOnly onDate)
    {
        return await _db.GstRules
            .Where(r => r.Active)
            .Where(r => r.ServiceType == serviceType)
            .Where(r => r.ProviderIsGta == providerIsGta)
            .Where(r => r.IssuesConsignmentNote == issuesConsignmentNote)
            .Where(r => r.ProviderGstRegistered == providerGstRegistered)
            .Where(r => r.GtaTaxScheme == gtaTaxScheme)
            .Where(r => r.RecipientCategory == recipientCategory)
            .Where(r => r.PosRelation == posRelation)
            .Where(r => r.EffectiveFrom <= onDate)
            .Where(r => r.EffectiveTo == null || r.EffectiveTo >= onDate)
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefaultAsync();
    }

    public async Task<GstRule?> GetByIdAsync(Guid id)
        => await _db.GstRules.FirstOrDefaultAsync(r => r.Id == id);

    public async Task<IReadOnlyList<GstRule>> ListAsync(DateOnly? onDate, bool? active)
    {
        var q = _db.GstRules.AsQueryable();
        if (active.HasValue) q = q.Where(r => r.Active == active.Value);
        if (onDate.HasValue)
        {
            var d = onDate.Value;
            q = q.Where(r => r.EffectiveFrom <= d && (r.EffectiveTo == null || r.EffectiveTo >= d));
        }
        return await q.OrderByDescending(r => r.EffectiveFrom).ThenBy(r => r.Id).ToListAsync();
    }

    public async Task<GstRule> AddAsync(GstRule rule, int? createdByUserId)
    {
        rule.Id = rule.Id == Guid.Empty ? Guid.NewGuid() : rule.Id;
        rule.CreatedBy = createdByUserId;
        rule.CreatedAt = DateTime.UtcNow;
        rule.Active = true;

        _db.GstRules.Add(rule);
        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task<IReadOnlyList<GstRule>> AddManyAsync(IReadOnlyList<GstRule> rules, int? createdByUserId)
    {
        var now = DateTime.UtcNow;
        foreach (var r in rules)
        {
            r.Id = r.Id == Guid.Empty ? Guid.NewGuid() : r.Id;
            r.CreatedBy = createdByUserId;
            r.CreatedAt = now;
            r.Active = true;
        }
        _db.GstRules.AddRange(rules);
        await _db.SaveChangesAsync();
        return rules;
    }

    public async Task<GstRule> UpdateWindowAsync(Guid id, DateOnly? effectiveTo, bool? active, int? updatedByUserId)
    {
        var rule = await _db.GstRules.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new InvalidOperationException("Rule not found");

        if (effectiveTo.HasValue) rule.EffectiveTo = effectiveTo.Value;
        if (active.HasValue) rule.Active = active.Value;

        rule.UpdatedBy = updatedByUserId;
        rule.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return rule;
    }

    public async Task<bool> IsReferencedByAuditAsync(Guid id)
        => await _db.TaxCalculations.IgnoreQueryFilters().AnyAsync(c => c.RuleVersionId == id);
}
