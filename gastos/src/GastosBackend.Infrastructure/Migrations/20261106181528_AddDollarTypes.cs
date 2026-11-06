using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GastosBackend.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDollarTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DollarType",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "BlueExchangeRate",
                table: "Transactions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MepExchangeRate",
                table: "Transactions",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DollarType",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BlueExchangeRate",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "MepExchangeRate",
                table: "Transactions");
        }
    }
}
