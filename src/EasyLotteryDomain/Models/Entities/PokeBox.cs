using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EasyLotteryDomain.Models.Entities
{
    public enum PokeMode
    {
        Random,
        Manual
    }

    public enum PokeAnimation
    {
        Burst,
        Smoke,
        Flash,
        Bounce,
        Flip
    }

    public class PokeTemplate
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public int GridRows { get; set; } = 3;

        public int GridColumns { get; set; } = 3;

        public PokeMode Mode { get; set; } = PokeMode.Random;

        public bool AllowRePoking { get; set; } = false;

        /// <summary>0 means unlimited</summary>
        public int MaxPokeCount { get; set; } = 0;

        public string BackgroundImageUrl { get; set; } = "";

        public string FontFamily { get; set; } = "";

        public int OverlayWidth { get; set; } = 1920;

        public int OverlayHeight { get; set; } = 1080;

        public PokeAnimation Animation { get; set; } = PokeAnimation.Burst;

        public string PokeSoundUrl { get; set; } = "";

        public string OpenSoundUrl { get; set; } = "";

        public bool IsBuiltIn { get; set; } = false;

        public TemplatePublicationStatus PublicationStatus { get; set; } = TemplatePublicationStatus.Published;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<PokeCell> Cells { get; set; } = new();
    }

    public class PokeCell
    {
        [Key]
        public int Id { get; set; }

        public int TemplateId { get; set; }

        public int Index { get; set; }

        public string Title { get; set; } = "";

        public string SubTitle { get; set; } = "";

        public string ImageUrl { get; set; } = "";

        public string RevealedImageUrl { get; set; } = "";

        public string RevealedColor { get; set; } = "#cccccc";

        public bool IsRevealed { get; set; } = false;

        public DateTime? RevealedAt { get; set; }

        [JsonIgnore]
        public PokeTemplate Template { get; set; } = null!;
    }
}
