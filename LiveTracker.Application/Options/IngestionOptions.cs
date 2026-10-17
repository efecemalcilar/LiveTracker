using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveTracker.Application.Options
{
    public sealed class IngestionOptions
    {
        public const string SectionName = "Ingestion";

        [Required, Url]
        public string FeedUrl { get; set; } = string.Empty;

        [Range(1,3600)]
        public int PollIntervalSeconds { get; set; } = 3;

        [Range(1,120)]
        public int RequestTimeoutSeconds { get; set; } = 10;

        [Required]
        public string ClientName { get; set; } = string.Empty;  // entur istemcilerden kendilerini tanıtan bir başlık bekliyor. (ET- Client Name)

        /// Veritabani kapaliyken bellekte bekletilecek en fazla kayit sayisi.
        [Range(1000, 5_000_000)]
        public int MaxBufferedRows { get; set; } = 200_000;
    }
}
