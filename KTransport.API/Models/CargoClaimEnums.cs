namespace KTransport.API.Models
{
    public enum ClaimType
    {
        Damage = 0,
        Shortage = 1,
        TotalLoss = 2,
        Delay = 3,
        Accident = 4
    }

    public enum ClaimStatus
    {
        Reported = 0,
        Investigating = 1,
        Approved = 2,
        Rejected = 3,
        Settled = 4
    }
}
