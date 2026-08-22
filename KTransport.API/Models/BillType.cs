using System.Collections.Generic;

namespace KTransport.API.Models
{
    public partial class BillType
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;

        // Navigation properties
        public virtual ICollection<ChallanDetail> ChallanDetails { get; set; } = new List<ChallanDetail>();
    }
}