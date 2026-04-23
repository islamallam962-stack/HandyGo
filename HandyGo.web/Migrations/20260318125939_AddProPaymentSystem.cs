using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HandyGo.web.Migrations
{

    public partial class AddProPaymentSystem : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "NetToTechnician",
                table: "Requests",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Requests",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformCommission",
                table: "Requests",
                type: "numeric(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NetToTechnician",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "PlatformCommission",
                table: "Requests");
        }
    }
}

