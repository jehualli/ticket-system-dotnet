using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Jehualli.TicketSystem.Api.Data.Migrations
{
    /// <summary>
    /// Adds dbo.usp_TicketCounts, used by the dashboard: ticket totals grouped by status and by priority.
    /// </summary>
    public partial class AddTicketCountsProcedure : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR ALTER PROCEDURE dbo.usp_TicketCounts
                AS
                BEGIN
                    SET NOCOUNT ON;

                    SELECT N'Status' AS Kind, t.Status AS Value, COUNT(*) AS Total
                    FROM dbo.Tickets AS t
                    GROUP BY t.Status

                    UNION ALL

                    SELECT N'Priority', t.Priority, COUNT(*)
                    FROM dbo.Tickets AS t
                    GROUP BY t.Priority;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_TicketCounts;");
        }
    }
}
