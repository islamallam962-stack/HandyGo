using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyGo.web.Migrations
{

    public partial class AddPaymentSystem : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "WalletBalance",
                table: "Users",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "Requests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Requests",
                type: "numeric(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WalletBalance",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Requests");
        }
    }
}

