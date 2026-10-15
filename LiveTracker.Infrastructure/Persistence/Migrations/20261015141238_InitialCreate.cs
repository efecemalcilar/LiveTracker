using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LiveTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vehicle_positions",
                columns: table => new
                {
                    entity_id = table.Column<string>(type: "text", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    vehicle_id = table.Column<string>(type: "text", nullable: false),
                    trip_id = table.Column<string>(type: "text", nullable: true),
                    route_id = table.Column<string>(type: "text", nullable: true),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    bearing = table.Column<float>(type: "real", nullable: true),
                    current_status = table.Column<short>(type: "smallint", nullable: true),
                    stop_id = table.Column<string>(type: "text", nullable: true),
                    occupancy_status = table.Column<short>(type: "smallint", nullable: true),
                    ingested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicle_positions", x => new { x.entity_id, x.recorded_at });
                });

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_positions_vehicle_time",
                table: "vehicle_positions",
                columns: new[] { "vehicle_id", "recorded_at" },
                descending: new[] { false, true });
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb;");
            migrationBuilder.Sql(@"SELECT create_hypertable('vehicle_positions', 'recorded_at',
                chunk_time_interval => INTERVAL '15 minutes', if_not_exists => TRUE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vehicle_positions");
        }
    }
}
