using LiveTracker.Domain;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Infrastructure.Persistence
{
    public sealed class LiveTrackerDbContext : DbContext
    {
        public LiveTrackerDbContext(DbContextOptions<LiveTrackerDbContext> options) : base(options) { }

        public DbSet<VehiclePosition> VehiclePosition => Set<VehiclePosition>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            b.Entity<VehiclePosition>(e =>
            {
                e.ToTable("vehicle_positions");
                e.HasKey(x => new { x.EntityId, x.RecordedAt });

                e.Property(x => x.EntityId).HasColumnName("entity_id");
                e.Property(x => x.RecordedAt).HasColumnName("recorded_at");
                e.Property(x => x.VehicleId).HasColumnName("vehicle_id");
                e.Property(x => x.TripId).HasColumnName("trip_id");
                e.Property(x => x.RouteId).HasColumnName("route_id");
                e.Property(x => x.Latitude).HasColumnName("latitude");
                e.Property(x => x.Longitude).HasColumnName("longitude");
                e.Property(x => x.Bearing).HasColumnName("bearing");
                e.Property(x => x.CurrentStatus).HasColumnName("current_status");
                e.Property(x => x.StopId).HasColumnName("stop_id");
                e.Property(x => x.OccupancyStatus).HasColumnName("occupancy_status");
                e.Property(x => x.IngestedAt).HasColumnName("ingested_at");

                e.HasIndex(x => new { x.VehicleId, x.RecordedAt })
                 .IsDescending(false, true)
                 .HasDatabaseName("ix_vehicle_positions_vehicle_time");
            });
        }
        
                
        
    }
}
