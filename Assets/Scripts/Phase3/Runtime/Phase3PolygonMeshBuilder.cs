using System;
using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3PolygonMeshData
    {
        public Phase3PolygonMeshData(Vector2[] vertices, int[] indices) { Vertices = vertices; Indices = indices; }
        public Vector2[] Vertices { get; }
        public int[] Indices { get; }
    }

    public static class Phase3PolygonMeshBuilder
    {
        public static Phase3PolygonMeshData Build(IReadOnlyList<Vector2> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Count < 3 || source.Count > 5) throw new ArgumentOutOfRangeException(nameof(source), "Polygon must contain 3 to 5 vertices.");
            var vertices = new Vector2[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                Vector2 point = source[i];
                if (!IsFinite(point)) throw new ArgumentOutOfRangeException(nameof(source), "Vertices must be finite.");
                for (int previous = 0; previous < i; previous++) if (vertices[previous] == point) throw new ArgumentException("Duplicate vertices are not allowed.", nameof(source));
                vertices[i] = point;
            }
            float signedTwiceArea = 0f;
            for (int i = 0; i < vertices.Length; i++) signedTwiceArea += Cross(vertices[i], vertices[(i + 1) % vertices.Length]);
            if (Mathf.Abs(signedTwiceArea) <= Mathf.Epsilon) throw new ArgumentException("Polygon area must be non-zero.", nameof(source));
            float winding = Mathf.Sign(signedTwiceArea);
            for (int i = 0; i < vertices.Length; i++)
            {
                float turn = Cross(vertices[(i + 1) % vertices.Length] - vertices[i], vertices[(i + 2) % vertices.Length] - vertices[(i + 1) % vertices.Length]);
                if (Mathf.Abs(turn) <= Mathf.Epsilon || Mathf.Sign(turn) != winding) throw new ArgumentException("Polygon must be strictly convex.", nameof(source));
            }
            var indices = new int[(vertices.Length - 2) * 3];
            for (int triangle = 0; triangle < vertices.Length - 2; triangle++)
            {
                int offset = triangle * 3;
                indices[offset] = 0;
                indices[offset + 1] = triangle + 1;
                indices[offset + 2] = triangle + 2;
            }
            return new Phase3PolygonMeshData(vertices, indices);
        }

        private static bool IsFinite(Vector2 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.y);
        private static float Cross(Vector2 left, Vector2 right) => left.x * right.y - left.y * right.x;
    }
}
