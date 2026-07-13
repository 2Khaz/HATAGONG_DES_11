namespace HATAGONG.Phase2.Rendering
{
    public readonly struct Phase2PaintStamp
    {
        public Phase2PaintStamp(float centerU, float centerV, float radiusRatio)
        {
            CenterU = centerU;
            CenterV = centerV;
            RadiusRatio = radiusRatio;
        }

        public float CenterU { get; }
        public float CenterV { get; }
        public float RadiusRatio { get; }
    }

    public readonly struct Phase2PaintBatchResult
    {
        public Phase2PaintBatchResult(int inputCount, int acceptedCount, int rejectedCount, bool gpuSubmitted)
        {
            InputCount = inputCount;
            AcceptedCount = acceptedCount;
            RejectedCount = rejectedCount;
            GpuSubmitted = gpuSubmitted;
        }

        public int InputCount { get; }
        public int AcceptedCount { get; }
        public int RejectedCount { get; }
        public bool GpuSubmitted { get; }
    }
}
