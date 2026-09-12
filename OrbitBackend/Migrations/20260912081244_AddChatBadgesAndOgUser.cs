using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrbitBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddChatBadgesAndOgUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SenderBadge",
                table: "ChatMessages",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOgUser",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SenderBadge",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "IsOgUser",
                table: "AspNetUsers");
        }
    }
}
