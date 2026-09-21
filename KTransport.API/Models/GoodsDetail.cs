using System;
using System.Collections.Generic;
using KTransport.API.Common;
using KTransport.API.Services;

namespace KTransport.API.Models;

public partial class GoodsDetail : ITenantScopedEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public int BillId { get; set; }

    public string? Article { get; set; }

    public string? Description { get; set; }

    public decimal? Weight { get; set; }

    public decimal? Rate { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual GstBill Bill { get; set; } = null!;
}
