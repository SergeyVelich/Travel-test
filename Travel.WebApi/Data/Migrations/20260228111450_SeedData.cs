using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Travel.WebApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Items",
                columns: new[] { "Id", "Name", "Price" },
                values: new object[,]
                {
                    { 1, "Standard Seat", 49.99m },
                    { 2, "Extra Legroom", 79.99m },
                    { 3, "Window Seat", 59.99m },
                    { 4, "Flight Meal", 12.50m },
                    { 5, "Priority Boarding", 25.00m },
                    { 6, "Checked Baggage", 35.00m },
                    { 7, "Travel Insurance", 19.99m },
                    { 8, "Lounge Access", 29.99m },
                    { 9, "Seat Selection", 9.99m },
                    { 10, "In-flight Wifi", 7.99m }
                });

            migrationBuilder.InsertData(
                table: "Inventory",
                columns: new[] { "ItemId", "InStock", "Reserved", "InTransit" },
                values: new object[,]
                {
                    { 1, 100, 20, 0 },
                    { 2, 100, 20, 0 },
                    { 3, 100, 20, 0 },
                    { 4, 100, 20, 0 },
                    { 5, 100, 20, 0 },
                    { 6, 100, 20, 0 },
                    { 7, 100, 20, 0 },
                    { 8, 100, 20, 0 },
                    { 9, 100, 20, 0 },
                    { 10, 100, 20, 0 }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            for (int id = 1; id <= 10; id++)
            {
                migrationBuilder.DeleteData(
                    table: "Items",
                    keyColumn: "Id",
                    keyValue: id);
            }

            for (int id = 1; id <= 10; id++)
            {
                migrationBuilder.DeleteData(
                    table: "Inventory",
                    keyColumn: "ItemId",
                    keyValue: id);
            }
        }
    }
}