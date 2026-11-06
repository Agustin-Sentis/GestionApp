using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastosBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CaseInsensitiveCategoryNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId_Name",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId",
                table: "Categories",
                column: "UserId");

            // Categories that only differ in letter case ("Comida" / "comida") would make the new unique index fail.
            // Keep the oldest one as it is and rename the others with a numeric suffix: "comida (2)", "comida (3)"...
            // The name is cut so that name + suffix still fits in the 60 characters of the column.
            migrationBuilder.Sql("""
                UPDATE "Categories" AS c
                SET "Name" = left(c."Name", 60 - length(' (' || d.rn || ')')) || ' (' || d.rn || ')'
                FROM (
                    SELECT "Id",
                           row_number() OVER (PARTITION BY "UserId", lower("Name") ORDER BY "CreatedDate", "Id") AS rn
                    FROM "Categories"
                    WHERE "IsDeleted" = false
                ) AS d
                WHERE c."Id" = d."Id" AND d.rn > 1;
                """);

            // EF Core cannot model an index on an expression, so it is created with raw SQL (not part of the model snapshot).
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Categories_UserId_LowerName"
                ON "Categories" ("UserId", lower("Name"))
                WHERE "IsDeleted" = false;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX "IX_Categories_UserId_LowerName";""");

            migrationBuilder.DropIndex(
                name: "IX_Categories_UserId",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_UserId_Name",
                table: "Categories",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }
    }
}
