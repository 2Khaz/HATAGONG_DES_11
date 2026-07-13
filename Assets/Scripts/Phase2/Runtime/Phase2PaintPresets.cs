namespace HATAGONG.Phase2
{
    public static class Phase2PaintPresets
    {
        public static Phase2PaintConfig Easy => Create(0.085d);
        public static Phase2PaintConfig Normal => Create(0.075d);
        public static Phase2PaintConfig Hard => Create(0.065d);

        private static Phase2PaintConfig Create(double radiusRatio)
        {
            return new Phase2PaintConfig(
                width: 128,
                height: 128,
                clearRatio: 0.99d,
                easyRadiusRatio: radiusRatio,
                normalRadiusRatio: radiusRatio,
                hardRadiusRatio: radiusRatio,
                stampSpacingRatio: 0.4d,
                coverageScoreBudget: 500,
                quarterMilestoneBonus: 100,
                halfMilestoneBonus: 150,
                threeQuarterMilestoneBonus: 200,
                clearBonus: 500);
        }
    }
}
