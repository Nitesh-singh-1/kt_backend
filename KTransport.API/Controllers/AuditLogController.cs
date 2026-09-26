using System;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Authorization;
using KTransport.API.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [RequireSuperUser]
    public class AuditLogController : ControllerBase
    {
        private readonly KTransportDbContext _context;

        public AuditLogController(KTransportDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get the current tenant's audit log, most recent first (Super User only).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAuditLogs([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] string? action = null)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 200 ? 25 : pageSize;

            var query = _context.AuditLogs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            var totalCount = await query.CountAsync();

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new
                {
                    a.Id,
                    a.UserId,
                    a.Username,
                    a.Action,
                    a.Success,
                    a.EntityType,
                    a.EntityId,
                    a.Details,
                    a.IpAddress,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                totalCount,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                items
            });
        }
    }
}
