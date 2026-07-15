namespace HATAGONG.GameFlow
{
    public readonly struct GameRunContext
    {
        public GameRunContext(GameDifficulty difficulty) : this(difficulty, RequestType.Normal) { }

        public GameRunContext(GameDifficulty difficulty, RequestType requestType)
        {
            HasSelectedRequest = false;
            RequestId = string.Empty;
            Difficulty = difficulty;
            RequestType = requestType;
            PermanentSeed = Phase1Seed = Phase2Seed = Phase3Seed = 0;
        }

        public GameRunContext(string requestId, GameDifficulty difficulty, RequestType requestType,
            int permanentSeed, int phase1Seed, int phase2Seed, int phase3Seed)
        {
            HasSelectedRequest = true;
            RequestId = requestId ?? string.Empty;
            Difficulty = difficulty;
            RequestType = requestType;
            PermanentSeed = permanentSeed;
            Phase1Seed = phase1Seed;
            Phase2Seed = phase2Seed;
            Phase3Seed = phase3Seed;
        }

        public bool HasSelectedRequest { get; }
        public string RequestId { get; }
        public GameDifficulty Difficulty { get; }
        public RequestType RequestType { get; }
        public int PermanentSeed { get; }
        public int Phase1Seed { get; }
        public int Phase2Seed { get; }
        public int Phase3Seed { get; }

        public bool IsValid =>
            (Difficulty == GameDifficulty.Easy || Difficulty == GameDifficulty.Normal || Difficulty == GameDifficulty.Hard) &&
            (RequestType == RequestType.Normal || RequestType == RequestType.Sudden) &&
            (!HasSelectedRequest || (!string.IsNullOrWhiteSpace(RequestId) && PermanentSeed > 0 &&
                Phase1Seed > 0 && Phase2Seed > 0 && Phase3Seed > 0));
    }
}
