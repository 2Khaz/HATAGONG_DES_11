using System;
using System.Collections.Generic;

namespace HATAGONG.Phase2
{
    public sealed class Phase2StampInterpolator
    {
        private bool _hasPreviousPoint;
        private float _lastInputU;
        private float _lastInputV;
        private double _remainingDistance;

        public int Begin(float u, float v, List<(float u, float v)> outputBuffer)
        {
            if (outputBuffer == null) throw new ArgumentNullException(nameof(outputBuffer));
            outputBuffer.Clear();
            outputBuffer.Add((u, v));
            _hasPreviousPoint = true;
            _lastInputU = u;
            _lastInputV = v;
            _remainingDistance = 0d;
            return 1;
        }

        public int AppendSegment(float nextU, float nextV, float spacing, List<(float u, float v)> outputBuffer)
        {
            if (outputBuffer == null) throw new ArgumentNullException(nameof(outputBuffer));
            if (float.IsNaN(nextU) || float.IsNaN(nextV) || float.IsInfinity(nextU) || float.IsInfinity(nextV) ||
                float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing <= 0f)
            {
                return 0;
            }

            if (!_hasPreviousPoint)
            {
                return Begin(nextU, nextV, outputBuffer);
            }

            double dx = (double)nextU - _lastInputU;
            double dy = (double)nextV - _lastInputV;
            double segmentLength = Math.Sqrt(dx * dx + dy * dy);
            if (segmentLength <= 0d)
            {
                _lastInputU = nextU;
                _lastInputV = nextV;
                return 0;
            }

            int generated = 0;
            double distanceToNextStamp = spacing - _remainingDistance;
            double lastGeneratedDistance = -1d;
            while (distanceToNextStamp <= segmentLength)
            {
                double t = distanceToNextStamp / segmentLength;
                outputBuffer.Add(((float)(_lastInputU + dx * t), (float)(_lastInputV + dy * t)));
                generated++;
                lastGeneratedDistance = distanceToNextStamp;
                distanceToNextStamp += spacing;
            }

            _remainingDistance = generated > 0 ? segmentLength - lastGeneratedDistance : _remainingDistance + segmentLength;
            _lastInputU = nextU;
            _lastInputV = nextV;
            return generated;
        }

        public void End()
        {
            _hasPreviousPoint = false;
            _remainingDistance = 0d;
        }
    }
}
