using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisualSearch.Api.Migrations
{
    /// <inheritdoc />
    public partial class SetDefaultCrawlerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set default crawler_type to 'generic' for all existing providers where it's NULL
            migrationBuilder.Sql(
                "UPDATE providers SET crawler_type = 'generic' WHERE crawler_type IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert back to NULL (no-op, as we can't know which were originally NULL)
            // This is intentionally left as a no-op for safety
        }
    }
}
