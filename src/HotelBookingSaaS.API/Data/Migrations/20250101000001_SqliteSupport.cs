using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HotelBookingSaaS.API.Data.Migrations
{
    /// <summary>
    /// Adds SQLite-compatible schema (no SQL Server-specific features).
    /// EF Core auto-selects the right migration based on the active provider.
    /// This migration is a no-op — the real schema comes from InitialCreate
    /// which uses EnsureCreated/Migrate handled by EF Core's cross-provider support.
    /// </summary>
    public partial class SqliteSupport : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
