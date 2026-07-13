using System;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3GridPoint : IEquatable<Phase3GridPoint>, IComparable<Phase3GridPoint>
    {
        public Phase3GridPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public bool IsWithinLogicalGrid =>
            X >= 0 && X <= Phase3CoreConstants.LogicalGridSize &&
            Y >= 0 && Y <= Phase3CoreConstants.LogicalGridSize;

        public long DistanceSquaredTo(Phase3GridPoint other)
        {
            long dx = (long)other.X - X;
            long dy = (long)other.Y - Y;
            return dx * dx + dy * dy;
        }

        public Phase3GridPoint DifferenceFrom(Phase3GridPoint other)
        {
            return new Phase3GridPoint(X - other.X, Y - other.Y);
        }

        public int CompareTo(Phase3GridPoint other)
        {
            int xComparison = X.CompareTo(other.X);
            return xComparison != 0 ? xComparison : Y.CompareTo(other.Y);
        }

        public bool Equals(Phase3GridPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is Phase3GridPoint other && Equals(other);
        public override int GetHashCode() => unchecked((X * 397) ^ Y);
        public override string ToString() => $"({X}, {Y})";

        public static bool operator ==(Phase3GridPoint left, Phase3GridPoint right) => left.Equals(right);
        public static bool operator !=(Phase3GridPoint left, Phase3GridPoint right) => !left.Equals(right);
        public static bool operator <(Phase3GridPoint left, Phase3GridPoint right) => left.CompareTo(right) < 0;
        public static bool operator >(Phase3GridPoint left, Phase3GridPoint right) => left.CompareTo(right) > 0;
    }
}
