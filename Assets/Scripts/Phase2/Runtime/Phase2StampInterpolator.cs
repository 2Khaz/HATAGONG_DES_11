using System;
using System.Collections.Generic;

namespace HATAGONG.Phase2
{
    public sealed class Phase2StampInterpolator
    {
        private bool _hasPreviousPoint;
        private float _lastInputU;
        private float _lastInputV;
        private float _remainingDistance;

        public int Begin(float u, float v, List<(float u, float v)> outputBuffer)
        {
            if (outputBuffer == null) throw new ArgumentNullException(nameof(outputBuffer));
            outputBuffer.Clear();
            outputBuffer.Add((u, v));
            _hasPreviousPoint = true;
            _lastInputU = u;
            _lastInputV = v;
            _remainingDistance = 0f;
            return 1;
        }

        public int AppendSegment(float nextU, float nextV, float spacing, List<(float u, float v)> outputBuffer)
        {
            if (outputBuffer == null) throw new ArgumentNullException(nameof(outputBuffer));
            if (float.IsNaN(nextU) || float.IsNaN(nextV) || float.IsInfinity(nextU) || float.IsInfinity(nextV) || spacing <= 0f)
            {
                return 0;
            }

            if (!_hasPreviousPoint)
            {
                return Begin(nextU, nextV, outputBuffer);
            }

            float dx = nextU - _lastInputU;
            float dy = nextV - _lastInputV;
            float segmentLength = (float)Math.Sqrt(dx * dx + dy * dy);
            if (segmentLength <= 0f)
            {
                _lastInputU = nextU;
                _lastInputV = nextV;
                return 0;
            }

            float totalDistance = _remainingDistance + segmentLength;
            int generated = 0;
            while (totalDistance >= spacing)
            {
                float advance = spacing - _remainingDistance;
                float t = advance / segmentLength;
                outputBuffer.Add((_lastInputU + dx * t, _lastInputV + dy * t));
                generated++;
                totalDistance -= spacing;
                _remainingDistance = 0f;
            }

            _remainingDistance = totalDistance;
            _lastInputU = nextU;
            _lastInputV = nextV;
            return generated;
        }

        public void End()
        {
            _hasPreviousPoint = false;
            _remainingDistance = 0f;
        }
    }
}
