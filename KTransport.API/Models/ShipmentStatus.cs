namespace KTransport.API.Models
{
    public enum ShipmentStatus
    {
        Draft = 0,
        Booked = 1,
        Manifested = 2,
        InTransit = 3,
        OutForDelivery = 4,
        Delivered = 5,
        Cancelled = 6,
        Returned = 7
    }
}
