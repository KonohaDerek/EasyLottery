namespace EasyLotteryDomain.Models.Config
{
    public enum ActivityResultType
    {
        PokeBox,
        Roulette
    }

    public sealed class ActivityResultRecord
    {
        public int Id { get; set; }

        public ActivityResultType ActivityType { get; set; }

        public string ActivityName { get; set; } = "";

        public int TemplateId { get; set; }

        public Guid TemplatePublicId { get; set; }

        public DateTime ActivityDateUtc { get; set; } = DateTime.UtcNow;

        public string Summary { get; set; } = "";

        public List<ActivityResultItem> Items { get; set; } = new();
    }

    public sealed class ActivityResultItem
    {
        public int Order { get; set; }

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public string ImageUrl { get; set; } = "";

        public string Color { get; set; } = "";

        public DateTime? ResultedAtUtc { get; set; }
    }
}
