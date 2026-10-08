using System;

namespace KTransport.API.Models
{
    /// <summary>
    /// TASK-040 Phase 1: lightweight append-only log of permission dual-read
    /// parity mismatches between the JSON path (authoritative in Phase 1) and
    /// the five new shadow tables. The parity-report admin endpoint reads this.
    /// 6 columns, zero FKs — complies with DB hierarchy ADR.
    /// </summary>
    public class PermissionParityLog
    {
        public long Id { get; set; }

        public Guid TenantId { get; set; }

        public int? UserId { get; set; }

        public string? Role { get; set; }

        /// <summary>Comma-separated keys only in the JSON path.</summary>
        public string? JsonOnlyKeys { get; set; }

        /// <summary>Comma-separated keys only in the tables path.</summary>
        public string? TablesOnlyKeys { get; set; }

        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
    }
}
