using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application.Options
{
    public sealed class RetentionOptions
    {
        public const string SectionName = "Retention";

        public TimeSpan Window { get; set; } = TimeSpan.FromHours(3);   // Verinin tutulacağı süre

        public TimeSpan ChunkInterval { get; set; } = TimeSpan.FromMinutes(15); // Hypertable chunk aralığı

        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1); // Temizlik işinin çalışma sıklığı
    }
}
