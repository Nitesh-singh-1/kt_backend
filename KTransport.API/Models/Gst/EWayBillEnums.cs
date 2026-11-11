namespace KTransport.API.Models.Gst;

// NIC enumerations as published in the EWB API spec. Values are the exact
// strings the NIC API expects — do not localize or renumber.
public static class EwbSupplyType
{
    public const string Outward = "O";
    public const string Inward = "I";
}

public static class EwbSubSupplyType
{
    public const string Supply = "1";
    public const string Import = "2";
    public const string Export = "3";
    public const string JobWork = "4";
    public const string ForOwnUse = "5";
    public const string JobWorkReturns = "6";
    public const string SalesReturn = "7";
    public const string Others = "8";
    public const string SKDCKD = "9";
    public const string LineSales = "10";
    public const string Recipient = "11";
    public const string Exhibition = "12";
}

public static class EwbDocType
{
    public const string TaxInvoice = "INV";
    public const string BillOfSupply = "BIL";
    public const string DeliveryChallan = "CHL";
    public const string BillOfEntry = "BOE";
    public const string Others = "OTH";
}

public static class EwbTransactionType
{
    public const int Regular = 1;
    public const int BillToShipTo = 2;
    public const int BillFromDispatchFrom = 3;
    public const int Combination = 4;
}

public static class EwbVehicleType
{
    public const string Regular = "R";
    public const string OverDimensional = "O";
}

public static class EwbStatus
{
    public const string Active = "ACT";
    public const string Cancelled = "CNL";
    public const string Expired = "EXP";
}

public enum EwayBillSyncStatus
{
    LocalOnly = 0,
    FetchedFromNic = 1,
    GeneratedViaApi = 2,
    Cancelled = 3,
    PartBUpdated = 4,
    ValidityExtended = 5,
    Error = 99
}
