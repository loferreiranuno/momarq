using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisualSearch.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderCrawlerConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "crawler_config_json",
                table: "providers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "crawler_type",
                table: "providers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "crawler_config_json",
                table: "providers");

            migrationBuilder.DropColumn(
                name: "crawler_type",
                table: "providers");
        }
    }
}
