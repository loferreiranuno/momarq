using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisualSearch.Api.Migrations
{
    /// <inheritdoc />
    public partial class SetZaraHomeCrawlerType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Set Zara Home providers to use the 'zarahome' Playwright-based crawler
            migrationBuilder.Sql(
                "UPDATE providers SET crawler_type = 'zarahome' WHERE LOWER(name) LIKE '%zara%home%' OR LOWER(website_url) LIKE '%zarahome%'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert Zara Home providers back to generic crawler
            migrationBuilder.Sql(
                "UPDATE providers SET crawler_type = 'generic' WHERE LOWER(name) LIKE '%zara%home%' OR LOWER(website_url) LIKE '%zarahome%'");
        }
    }
}
