using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KeyloopScheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "customer_id",
                table: "appointments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.id);
                    table.ForeignKey(
                        name: "FK_customers_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealerships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_customer_id",
                table: "appointments",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_dealership_active",
                table: "customers",
                columns: new[] { "dealership_id", "is_active" });

            // Appointments predating the customer association cannot be attributed to a
            // customer and would violate the required FK below, so they are removed.
            migrationBuilder.Sql(
                "DELETE FROM appointments WHERE customer_id = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.AddForeignKey(
                name: "FK_appointments_customers_customer_id",
                table: "appointments",
                column: "customer_id",
                principalTable: "customers",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_appointments_customers_customer_id",
                table: "appointments");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropIndex(
                name: "IX_appointments_customer_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "customer_id",
                table: "appointments");
        }
    }
}
