using System;
using System.Collections.Generic;

namespace KTransport.API.Models;

public partial class Charge
{
    public int Id { get; set; }

    public int BillId { get; set; }

    public decimal? Freight { get; set; }

    public decimal? ServiceCharge { get; set; }

    public decimal? DdCharge { get; set; }

    public decimal? Hamali { get; set; }

    public decimal? OtherCharge { get; set; }

    public decimal? StCharge { get; set; }

    public decimal? GrandTotal { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual GstBill Bill { get; set; } = null!;
}
