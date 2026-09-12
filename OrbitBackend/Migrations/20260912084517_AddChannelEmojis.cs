using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrbitBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelEmojis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChannelEmojis",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    EmojiValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsCustomImage = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChannelId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChannelEmojis", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChannelEmojis_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChannelEmojis_ChannelId",
                table: "ChannelEmojis",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_ChannelEmojis_ChannelId_Name",
                table: "ChannelEmojis",
                columns: new[] { "ChannelId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChannelEmojis");
        }
    }
}
