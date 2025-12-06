using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VisualSearch.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalPathToProductImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "local_path",
                table: "product_images",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "local_path",
                table: "product_images");
        }
    }
}
