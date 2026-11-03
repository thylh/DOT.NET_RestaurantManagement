using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveOrderHistoryAfterProfileRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "CreatedByEmployeeId",
                table: "RestaurantOrders",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "CreatedByEmployeeNameSnapshot",
                table: "RestaurantOrders",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CreatedByUserId",
                table: "RestaurantOrders",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE `RestaurantOrders` AS restaurantOrder " +
                "LEFT JOIN `Employees` AS employee ON restaurantOrder.`CreatedByEmployeeId` = employee.`Id` " +
                "LEFT JOIN `UserAccounts` AS account ON account.`EmployeeId` = employee.`Id` " +
                "SET restaurantOrder.`CreatedByEmployeeNameSnapshot` = COALESCE(employee.`FullName`, ''), " +
                "restaurantOrder.`CreatedByUserId` = account.`Id`");

            migrationBuilder.CreateIndex(
                name: "IX_RestaurantOrders_CreatedByUserId",
                table: "RestaurantOrders",
                column: "CreatedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_RestaurantOrders_UserAccounts_CreatedByUserId",
                table: "RestaurantOrders",
                column: "CreatedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RestaurantOrders_UserAccounts_CreatedByUserId",
                table: "RestaurantOrders");

            migrationBuilder.DropIndex(
                name: "IX_RestaurantOrders_CreatedByUserId",
                table: "RestaurantOrders");

            migrationBuilder.DropColumn(
                name: "CreatedByEmployeeNameSnapshot",
                table: "RestaurantOrders");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "RestaurantOrders");

            migrationBuilder.AlterColumn<int>(
                name: "CreatedByEmployeeId",
                table: "RestaurantOrders",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
