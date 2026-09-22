namespace KTransport.API.Models
{
    public enum TripStatus
    {
        Draft = 0,
        Loading = 1,
        Dispatched = 2,
        InTransit = 3,
        Arrived = 4,
        Unloaded = 5,
        Completed = 6,
        Cancelled = 7
    }

    public enum TripExpenseType
    {
        Fuel = 0,
        Toll = 1,
        DriverAllowance = 2,
        PoliceKharcha = 3,
        VehicleRepair = 4,
        LoadingCharges = 5,
        UnloadingCharges = 6,
        Parking = 7,
        Misc = 8
    }
}
