namespace KTransport.API.Models
{
    /// <summary>Which money-in flow produced a <see cref="ShipmentPayment"/> ledger row.</summary>
    public enum PaymentSource
    {
        /// <summary>Collected via the Money Receipts screen (Paid bilty).</summary>
        Receipt = 0,

        /// <summary>Captured at booking time as an on-spot advance (Paid bilty).</summary>
        OnSpot = 1,

        /// <summary>Marked paid through a Bill Book invoice (TBB bilty).</summary>
        BillBook = 2,

        /// <summary>Received during delivery settlement (To Pay bilty).</summary>
        DeliverySettlement = 3,

        /// <summary>Synthetic row created by the one-time backfill of pre-ledger paid amounts.</summary>
        OpeningBackfill = 4,
    }
}
