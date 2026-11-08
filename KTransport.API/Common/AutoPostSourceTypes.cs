namespace KTransport.API.Common
{
    /// <summary>
    /// Canonical <c>source_type</c> strings that link an operational document to its auto-posted
    /// voucher (ADR accounting-autoposting-phase2.md Q3 / contract internal_posting_engine).
    /// Used by both the source-service enqueue hooks and the posting engine's builders.
    /// </summary>
    public static class AutoPostSourceTypes
    {
        public const string Invoice = "Invoice";               // Sales voucher
        public const string InvoicePayment = "InvoicePayment"; // Receipt (source_id = Invoice.Id)
        public const string Shipment = "Shipment";             // Receipt (shipment settlement)
        public const string TripExpense = "TripExpense";       // Payment
        public const string LorryHireContract = "LorryHireContract"; // Purchase (hire memo)
        public const string LorryHirePayment = "LorryHirePayment";   // Payment (source_id = LorryHireContract.Id)
        public const string DriverLedgerEntry = "DriverLedgerEntry"; // Payment(advance) / Receipt(recovery)
    }
}
