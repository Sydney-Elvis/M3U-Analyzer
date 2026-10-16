using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace M3UAnalyzer.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sources",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    DisplayLocation = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ProtectedSecret = table.Column<string>(type: "TEXT", nullable: true),
                    IncludeVod = table.Column<bool>(type: "INTEGER", nullable: false),
                    IncludeSeries = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentSnapshotId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Snapshots",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FinishedUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    BytesFetched = table.Column<long>(type: "INTEGER", nullable: true),
                    CoreReference = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    RulesVersion = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Snapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Snapshots_Sources_SourceId",
                        column: x => x.SourceId,
                        principalTable: "Sources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CatalogGroups",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RawName = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderCategoryId = table.Column<string>(type: "TEXT", nullable: true),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CatalogGroups_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportWarnings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Section = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false),
                    SamplesJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportWarnings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportWarnings_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ContentTypeBasis = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    RawTitle = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayTitle = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayTitleIsFallback = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProviderItemId = table.Column<string>(type: "TEXT", nullable: true),
                    StreamKey = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    TvgId = table.Column<string>(type: "TEXT", nullable: true),
                    TvgName = table.Column<string>(type: "TEXT", nullable: true),
                    LogoUrl = table.Column<string>(type: "TEXT", nullable: true),
                    AttributesJson = table.Column<string>(type: "TEXT", nullable: true),
                    WithheldAttributeKeysJson = table.Column<string>(type: "TEXT", nullable: true),
                    Year = table.Column<int>(type: "INTEGER", nullable: true),
                    ContainerExtension = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SnapshotSections",
                columns: table => new
                {
                    SnapshotId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    Section = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    ItemCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SnapshotSections", x => new { x.SnapshotId, x.Section });
                    table.ForeignKey(
                        name: "FK_SnapshotSections_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Episodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SnapshotId = table.Column<string>(type: "TEXT", maxLength: 36, nullable: false),
                    SeriesItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    Season = table.Column<int>(type: "INTEGER", nullable: true),
                    EpisodeNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    ProviderEpisodeId = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    ContainerExtension = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Episodes_Items_SeriesItemId",
                        column: x => x.SeriesItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Episodes_Snapshots_SnapshotId",
                        column: x => x.SnapshotId,
                        principalTable: "Snapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItemGroups",
                columns: table => new
                {
                    ItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItemGroups", x => new { x.ItemId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_ItemGroups_CatalogGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "CatalogGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItemGroups_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SeriesEpisodeLoads",
                columns: table => new
                {
                    SeriesItemId = table.Column<long>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    LoadedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EpisodeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeriesEpisodeLoads", x => x.SeriesItemId);
                    table.ForeignKey(
                        name: "FK_SeriesEpisodeLoads_Items_SeriesItemId",
                        column: x => x.SeriesItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CatalogGroups_SnapshotId_ContentType_RawName",
                table: "CatalogGroups",
                columns: new[] { "SnapshotId", "ContentType", "RawName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeriesItemId",
                table: "Episodes",
                column: "SeriesItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SnapshotId",
                table: "Episodes",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportWarnings_SnapshotId",
                table: "ImportWarnings",
                column: "SnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_ItemGroups_GroupId",
                table: "ItemGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_SnapshotId_ContentType",
                table: "Items",
                columns: new[] { "SnapshotId", "ContentType" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_SnapshotId_ProviderItemId",
                table: "Items",
                columns: new[] { "SnapshotId", "ProviderItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_SnapshotId_StreamKey",
                table: "Items",
                columns: new[] { "SnapshotId", "StreamKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Items_SnapshotId_TvgId",
                table: "Items",
                columns: new[] { "SnapshotId", "TvgId" });

            migrationBuilder.CreateIndex(
                name: "IX_Snapshots_SourceId_StartedUtc",
                table: "Snapshots",
                columns: new[] { "SourceId", "StartedUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Episodes");

            migrationBuilder.DropTable(
                name: "ImportWarnings");

            migrationBuilder.DropTable(
                name: "ItemGroups");

            migrationBuilder.DropTable(
                name: "SeriesEpisodeLoads");

            migrationBuilder.DropTable(
                name: "SnapshotSections");

            migrationBuilder.DropTable(
                name: "CatalogGroups");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Snapshots");

            migrationBuilder.DropTable(
                name: "Sources");
        }
    }
}
