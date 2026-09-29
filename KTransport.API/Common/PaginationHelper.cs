using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace KTransport.API.Common
{
    /// <summary>
    /// TASK-011c: header-based pagination that preserves the response body shape (still
    /// a plain JSON array), so existing clients that ignore <c>page</c> / <c>pageSize</c>
    /// keep working. Callers pass the full in-memory list they already loaded from the
    /// service, the <see cref="HttpContext"/>, and the query parameters — this helper
    /// stamps <c>X-Total-Count</c> on the response and returns the requested slice.
    /// </summary>
    public static class PaginationHelper
    {
        // Cap pageSize server-side so a client can't request "give me a million rows"
        // and drag the process down. If a caller wants everything, they omit both
        // params and get the full list (existing behaviour).
        public const int MaxPageSize = 500;

        public static IReadOnlyList<T> Paginate<T>(
            IReadOnlyList<T> all,
            HttpContext context,
            int? page,
            int? pageSize)
        {
            var total = all.Count;

            // Always advertise the true total so a client can render "showing 20 of 347".
            context.Response.Headers["X-Total-Count"] = total.ToString();
            // Expose the header to browsers on cross-origin responses. Cheap belt-and-braces.
            context.Response.Headers.Append("Access-Control-Expose-Headers", "X-Total-Count");

            if (!page.HasValue || !pageSize.HasValue) return all;

            var normalizedPage = page.Value < 1 ? 1 : page.Value;
            var normalizedSize = pageSize.Value < 1 ? 1 : (pageSize.Value > MaxPageSize ? MaxPageSize : pageSize.Value);

            return all
                .Skip((normalizedPage - 1) * normalizedSize)
                .Take(normalizedSize)
                .ToList();
        }
    }
}
