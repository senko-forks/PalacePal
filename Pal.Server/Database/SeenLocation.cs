namespace Pal.Server.Database
{
    public sealed class SeenLocation
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Database.Account Account { get; set; } = null!;
        public Guid PalaceLocationId { get; set; }
        public ServerLocation ServerLocation { get; set; } = null!;
        public DateTime FirstSeenAt { get; set; }

        private SeenLocation() { }

        public SeenLocation(Database.Account account, Guid palaceLocationId)
        {
            Account = account;
            PalaceLocationId = palaceLocationId;
            FirstSeenAt = DateTime.Now;
        }
    }
}
