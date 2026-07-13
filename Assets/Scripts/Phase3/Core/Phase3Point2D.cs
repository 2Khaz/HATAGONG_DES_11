using System;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3Point2D : IEquatable<Phase3Point2D>
    {
        public Phase3Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }
        public bool IsFinite => IsFiniteValue(X) && IsFiniteValue(Y);

        public double DistanceSquaredTo(Phase3Point2D other)
        {
            double dx = other.X - X;
            double dy = other.Y - Y;
            return dx * dx + dy * dy;
        }

        public bool Equals(Phase3Point2D other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Phase3Point2D other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X:R}, {Y:R})";

        public static Phase3Point2D operator +(Phase3Point2D left, Phase3Point2D right) =>
            new Phase3Point2D(left.X + right.X, left.Y + right.Y);

        public static Phase3Point2D operator -(Phase3Point2D left, Phase3Point2D right) =>
            new Phase3Point2D(left.X - right.X, left.Y - right.Y);

        public static Phase3Point2D operator *(Phase3Point2D point, double scalar) =>
            new Phase3Point2D(point.X * scalar, point.Y * scalar);

        public static Phase3Point2D operator /(Phase3Point2D point, double scalar) =>
            new Phase3Point2D(point.X / scalar, point.Y / scalar);

        public static bool operator ==(Phase3Point2D left, Phase3Point2D right) => left.Equals(right);
        public static bool operator !=(Phase3Point2D left, Phase3Point2D right) => !left.Equals(right);

        public static bool IsFiniteValue(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
