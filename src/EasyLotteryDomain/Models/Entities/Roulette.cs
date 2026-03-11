using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EasyLotteryDomain.Models.Entities
{
    public class RouletteTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        /// <summary>Number of segments (e.g. 6, 8, 12, 24)</summary>
        public int SegmentCount { get; set; } = 8;

        /// <summary>Total spin duration in seconds</summary>
        public double SpinDurationSec { get; set; } = 5.0;

        /// <summary>CSS easing function name, e.g. "ease-out-cubic"</summary>
        public string EasingFunction { get; set; } = "ease-out-cubic";

        /// <summary>Initial rotation angle in degrees (where pointer points at start)</summary>
        public double InitialAngleDeg { get; set; } = 0;

        public string CenterImageUrl { get; set; } = "";

        public string BackgroundImageUrl { get; set; } = "";

        public string PointerImageUrl { get; set; } = "";

        public string SpinSoundUrl { get; set; } = "";

        public string WinSoundUrl { get; set; } = "";

        public bool IsBuiltIn { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<RouletteSegment> Segments { get; set; } = new();
    }

    public class RouletteSegment
    {
        [Key]
        public int Id { get; set; }

        public int TemplateId { get; set; }

        public int Index { get; set; }

        public string Title { get; set; } = "";

        public string ImageUrl { get; set; } = "";

        /// <summary>Background color for this segment (hex)</summary>
        public string Color { get; set; } = "#cccccc";

        /// <summary>Win probability in percent (0 = equal distribution)</summary>
        public double Probability { get; set; } = 0;

        [JsonIgnore]
        public RouletteTemplate Template { get; set; } = null!;
    }

    public class SpinResult
    {
        /// <summary>Zero-based index of the winning segment</summary>
        public int SegmentIndex { get; set; }

        /// <summary>Total degrees the wheel should rotate</summary>
        public double TotalRotationDeg { get; set; }

        /// <summary>Title of the winning segment</summary>
        public string SegmentTitle { get; set; } = "";
    }
}
