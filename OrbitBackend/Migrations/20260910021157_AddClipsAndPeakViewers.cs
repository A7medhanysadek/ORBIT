using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrbitBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddClipsAndPeakViewers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PeakViewers",
                table: "LiveStreams",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Clips",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VideoUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: false),
                    ViewCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ChannelId = table.Column<int>(type: "int", nullable: false),
                    LiveStreamId = table.Column<int>(type: "int", nullable: true),
                    CategoryId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clips", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clips_AspNetUsers_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Clips_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Clips_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Clips_LiveStreams_LiveStreamId",
                        column: x => x.LiveStreamId,
                        principalTable: "LiveStreams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_LiveStreams_PeakViewers",
                table: "LiveStreams",
                column: "PeakViewers");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CategoryId",
                table: "Clips",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CategoryId_ViewCount",
                table: "Clips",
                columns: new[] { "CategoryId", "ViewCount" });

            migrationBuilder.CreateIndex(
                name: "IX_Clips_ChannelId",
                table: "Clips",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CreatedAt",
                table: "Clips",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_CreatorId",
                table: "Clips",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_LiveStreamId",
                table: "Clips",
                column: "LiveStreamId");

            migrationBuilder.CreateIndex(
                name: "IX_Clips_ViewCount",
                table: "Clips",
                column: "ViewCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clips");

            migrationBuilder.DropIndex(
                name: "IX_LiveStreams_PeakViewers",
                table: "LiveStreams");

            migrationBuilder.DropColumn(
                name: "PeakViewers",
                table: "LiveStreams");
        }
    }
}
