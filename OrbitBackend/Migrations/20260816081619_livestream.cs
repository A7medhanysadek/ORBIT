using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrbitBackend.Migrations
{
    
    public partial class livestream : Migration
    {
        
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StreamKey",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LiveStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsLive = table.Column<bool>(type: "bit", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StreamerId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LiveStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LiveStreams_AspNetUsers_StreamerId",
                        column: x => x.StreamerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_StreamKey",
                table: "AspNetUsers",
                column: "StreamKey",
                unique: true,
                filter: "[StreamKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LiveStreams_IsLive",
                table: "LiveStreams",
                column: "IsLive");

            migrationBuilder.CreateIndex(
                name: "IX_LiveStreams_StreamerId",
                table: "LiveStreams",
                column: "StreamerId");
        }

        
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LiveStreams");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_StreamKey",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "StreamKey",
                table: "AspNetUsers");
        }
    }
}
