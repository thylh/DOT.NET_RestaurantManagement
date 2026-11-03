using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RestaurantManagement.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class SeparateAccountAndEmployeeProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "UserAccounts",
                type: "varchar(254)",
                maxLength: 254,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullName",
                table: "UserAccounts",
                type: "varchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "UserAccounts",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE `UserAccounts` AS account " +
                "LEFT JOIN `Employees` AS employee ON account.`EmployeeId` = employee.`Id` " +
                "SET account.`FullName` = COALESCE(NULLIF(employee.`FullName`, ''), account.`Username`)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "FullName",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "UserAccounts");
        }
    }
}
