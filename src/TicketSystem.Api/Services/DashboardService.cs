using Jehualli.TicketSystem.Api.Data;
using Jehualli.TicketSystem.Api.Dtos;
using Jehualli.TicketSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Jehualli.TicketSystem.Api.Services;

public class DashboardService(AppDbContext db)
{
    // One row per (Kind, Value) pair, e.g. ("Status", "Open", 4), returned by dbo.usp_TicketCounts.
    public record TicketCountRow(string Kind, string Value, int Total);

    public async Task<DashboardStatsResponse> GetStatsAsync(CancellationToken ct = default)
    {
        // The counts come from a stored procedure (see the AddTicketCountsProcedure migration).
        // EXEC can't be composed into a larger query, so it's materialised with ToListAsync as-is.
        var rows = await db.Database
            .SqlQuery<TicketCountRow>($"EXEC dbo.usp_TicketCounts")
            .ToListAsync(ct);

        var byStatus = Enum.GetValues<TicketStatus>().ToDictionary(s => s, s => Count(rows, "Status", s.ToString()));
        var byPriority = Enum.GetValues<TicketPriority>().ToDictionary(p => p, p => Count(rows, "Priority", p.ToString()));

        var recent = await db.Tickets.AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Select(Mapping.ToTicketResponse)
            .ToListAsync(ct);

        return new DashboardStatsResponse(byStatus.Values.Sum(), byStatus, byPriority, recent);
    }

    private static int Count(List<TicketCountRow> rows, string kind, string value) =>
        rows.FirstOrDefault(r => r.Kind == kind && r.Value == value)?.Total ?? 0;
}
