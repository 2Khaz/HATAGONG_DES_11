namespace HATAGONG.GameFlow
{
    public readonly struct GameRunContext
    {
        public GameRunContext(GameDifficulty difficulty)
        {
            Difficulty = difficulty;
        }

        public GameDifficulty Difficulty { get; }

        public bool IsValid =>
            Difficulty == GameDifficulty.Easy ||
            Difficulty == GameDifficulty.Normal ||
            Difficulty == GameDifficulty.Hard;
    }
}
