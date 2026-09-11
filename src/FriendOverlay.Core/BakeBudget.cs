namespace FriendOverlay.Core
{
    public static class BakeBudget
    {
        public const int DefaultLimit = 2;

        private static int _frame = int.MinValue;
        private static int _used;

        public static int Limit { get; set; } = DefaultLimit;

        public static int Remaining
        {
            get
            {
                var lim = Limit < 0 ? 0 : Limit;
                return lim > _used ? lim - _used : 0;
            }
        }

        public static void BeginFrame(int frameCount)
        {
            if (frameCount == _frame)
                return;
            _frame = frameCount;
            _used = 0;
        }

        public static bool TryConsume()
        {
            if (Remaining <= 0)
                return false;
            _used++;
            return true;
        }

        public static void Reset()
        {
            _frame = int.MinValue;
            _used = 0;
        }
    }
}
