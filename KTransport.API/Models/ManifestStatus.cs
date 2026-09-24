namespace KTransport.API.Models
{
    public enum ManifestStatus
    {
        Draft = 0,
        Finalized = 1,
        Loaded = 2,
        Dispatched = 3,
        InTransit = 4,
        ArrivedAtHub = 5,
        Unloaded = 6,
        Closed = 7,
        Cancelled = 8
    }

    public enum ManifestItemStatus
    {
        Loaded = 0,
        ReceivedIntact = 1,
        ShortageReported = 2,
        ExcessReported = 3,
        DamagedReported = 4
    }
}
