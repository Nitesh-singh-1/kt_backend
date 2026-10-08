using System;
using System.Collections.Generic;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-041 Phase 1: lightweight append-only log of menu dual-read parity
    /// mismatches between the C# tree (authoritative in Phase 1) and the
    /// menu_items shadow table. The parity-report admin endpoint reads this.
    /// 6 non-identity columns, zero FKs — complies with DB hierarchy ADR.
    /// </summary>
    public class MenuParityLog
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public int? UserId { get; set; }

        public string? UserRole { get; set; }

        /// <summary>Keys the C# tree emitted that the tables path did not.</summary>
        public List<string> CodeOnlyKeys { get; set; } = new();

        /// <summary>Keys the tables path emitted that the C# tree did not.</summary>
        public List<string> TablesOnlyKeys { get; set; } = new();

        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    }
}
