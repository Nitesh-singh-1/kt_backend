using System.Collections.Generic;
using KTransport.API.Common;
using KTransport.API.Models;

namespace KTransport.API.Services
{
    /// <summary>One credit leg of the GST portion of a Sales voucher (well-known ledger key + amount).</summary>
    public readonly record struct GstCreditLeg(string LedgerKey, decimal Amount);

    /// <summary>
    /// The GST SEAM (ADR accounting-autoposting-phase2.md Q2 / Option B3). The auto-poster asks this
    /// resolver for the GST credit leg(s) of a Sales voucher instead of hard-coding them, so the flat
    /// single-Output-GST behaviour on THIS branch can be swapped for a CGST/SGST/IGST split resolver
    /// (backed by the <c>feature/gst-engine</c> <c>TaxCalculation</c> model) with NO change to the
    /// posting pipeline once that branch merges. DO NOT reference any gst-engine type here.
    /// </summary>
    public interface IGstPostingResolver
    {
        /// <summary>Returns the GST credit legs for an invoice, or an empty list when there is no GST leg.</summary>
        IReadOnlyList<GstCreditLeg> ResolveOutputGst(Invoice invoice);
    }

    /// <summary>
    /// Flat resolver for this branch: a single <c>OUTPUT_GST</c> credit = <see cref="Invoice.TaxAmount"/>.
    /// When <c>TaxAmount == 0</c> (RCM / exempt / without-GST billing) there is NO GST leg — the
    /// GST-correct posting for an RCM bill (GST-COMPLIANCE.md §4). The amount is read from the invoice
    /// as DATA, never recomputed from a hard-coded rate.
    /// </summary>
    public class FlatGstPostingResolver : IGstPostingResolver
    {
        public IReadOnlyList<GstCreditLeg> ResolveOutputGst(Invoice invoice)
        {
            var tax = invoice.TaxAmount;
            if (tax <= 0) return System.Array.Empty<GstCreditLeg>();
            return new[] { new GstCreditLeg(WellKnownLedgers.OutputGst, decimal.Round(tax, 2)) };
        }
    }
}
