namespace Pal.Server
{
    public class SeenLocation
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public Account Account { get; set; } = null!;
        public Guid PalaceLocationId { get; set; }
        public PalaceLocation PalaceLocation { get; set; } = null!;
        public DateTime FirstSeenAt { get; set; }

        protected SeenLocation() { }

        public SeenLocation(Account account, Guid palaceLocationId)
        {
            Account = account;
            PalaceLocationId = palaceLocationId;
            FirstSeenAt = DateTime.Now;
        }
    }
}
