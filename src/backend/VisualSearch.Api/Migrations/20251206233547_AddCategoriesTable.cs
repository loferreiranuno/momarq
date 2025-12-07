using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace VisualSearch.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoriesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_products_category",
                table: "products");

            migrationBuilder.DropColumn(
                name: "category",
                table: "products");

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "products",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    coco_class_id = table.Column<int>(type: "integer", nullable: false),
                    detection_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_categories_coco_class_id",
                table: "categories",
                column: "coco_class_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_detection_enabled",
                table: "categories",
                column: "detection_enabled");

            migrationBuilder.CreateIndex(
                name: "IX_categories_name",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_category_id",
                table: "products",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Seed COCO furniture classes
            var furnitureClasses = new (int CocoClassId, string Name, bool DetectionEnabled)[]
            {
                (56, "chair", true),
                (57, "couch", true),
                (58, "potted plant", true),
                (59, "bed", true),
                (60, "dining table", true),
                (61, "toilet", true),
                (62, "tv", true),
                (63, "laptop", true),
                (64, "mouse", false),
                (65, "remote", false),
                (66, "keyboard", false),
                (67, "cell phone", false),
                (68, "microwave", true),
                (69, "oven", true),
                (70, "toaster", false),
                (71, "sink", true),
                (72, "refrigerator", true),
                (73, "book", false),
                (74, "clock", true),
                (75, "vase", true),
                (76, "scissors", false),
                (77, "teddy bear", false),
                (78, "hair drier", false),
                (79, "toothbrush", false)
            };

            foreach (var (cocoClassId, name, detectionEnabled) in furnitureClasses)
            {
                migrationBuilder.InsertData(
                    table: "categories",
                    columns: new[] { "name", "coco_class_id", "detection_enabled" },
                    values: new object[] { name, cocoClassId, detectionEnabled });
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_category_id",
                table: "products");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropIndex(
                name: "IX_products_category_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "products");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "products",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_products_category",
                table: "products",
                column: "category");
        }
    }
}
