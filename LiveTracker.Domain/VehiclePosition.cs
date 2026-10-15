using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Domain
{
    public sealed class VehiclePosition
    {
        public string EntityId { get; set; } = null!;
        public DateTime RecordedAt { get; set; }          // UTC, konumun ölçüldüğü an
        public string VehicleId { get; set; } = null!;
        public string? TripId { get; set; }
        public string? RouteId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public float? Bearing { get; set; }
        public short? CurrentStatus { get; set; }
        public string? StopId { get; set; }
        public short? OccupancyStatus { get; set; }
        public DateTime IngestedAt { get; set; }          // UTC, bizim aldığımız an
    }
}
