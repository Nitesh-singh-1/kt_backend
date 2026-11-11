using System;
using System.Threading.Tasks;
using KTransport.API.DTOs.Gst;

namespace KTransport.API.Services.Gst;

public interface ITaxCalculationService
{
    /// <summary>
    /// Preview calculation — does NOT persist a tax_calculations row.
    /// Used by the Bilty UI as the operator types.
    /// Throws <see cref="GstRuleUnresolvedException"/> if the input tuple
    /// does not match exactly one active rule on the transaction date.
    /// </summary>
    Task<TaxCalculationResultDto> CalculateAsync(TaxCalculationInputDto input);

    /// <summary>
    /// Compute AND persist an immutable tax_calculations audit row for
    /// the current tenant. The result's RuleVersionId points at the
    /// GstRule row used; the row itself is immutable from here on
    /// (recalculation inserts a new row + supersedes the old one).
    /// </summary>
    Task<TaxCalculationDto> ApplyAsync(TaxCalculationApplyRequestDto request, int? calculatedByUserId);

    /// <summary>
    /// Re-run the calculation for an existing audit row. Keeps the
    /// original TransactionDate (so the rule effective on that date is
    /// still used — historical fidelity), inserts a new audit row, and
    /// updates the old row's SupersededBy to point at the new one.
    /// </summary>
    Task<TaxCalculationDto> RecalculateAsync(Guid taxCalculationId, int? calculatedByUserId);

    /// <summary>Fetch an existing audit row (tenant-scoped).</summary>
    Task<TaxCalculationDto?> GetAsync(Guid id);
}
