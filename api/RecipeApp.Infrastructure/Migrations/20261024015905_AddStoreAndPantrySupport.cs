using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipeApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreAndPantrySupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPantryStaple",
                table: "ShoppingListItems",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Store",
                table: "ShoppingListItems",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPantryStaple",
                table: "Ingredients",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PreferredStore",
                table: "Ingredients",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPantryStaple",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "Store",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "IsPantryStaple",
                table: "Ingredients");

            migrationBuilder.DropColumn(
                name: "PreferredStore",
                table: "Ingredients");
        }
    }
}
