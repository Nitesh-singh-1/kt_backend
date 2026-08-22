using System;
using System.Collections.Generic;

namespace KTransport.API.Models;

public partial class GoodsDetail
{
    public int Id { get; set; }

    public int BillId { get; set; }

    public string? Article { get; set; }

    public string? Description { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Rate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual GstBill Bill { get; set; } = null!;
}
