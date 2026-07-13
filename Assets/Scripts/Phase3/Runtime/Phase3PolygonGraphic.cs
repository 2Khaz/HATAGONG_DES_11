using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Phase3
{
    public sealed class Phase3PolygonGraphic : MaskableGraphic
    {
        [SerializeField] private Color outlineColor = Color.black;
        [SerializeField, Min(0f)] private float outlineWidth;
        private Vector2[] vertices = new Vector2[0];

        public IReadOnlyList<Vector2> Vertices => vertices;
        public Color OutlineColor { get => outlineColor; set { outlineColor = value; SetVerticesDirty(); } }
        public float OutlineWidth { get => outlineWidth; set { outlineWidth = Mathf.Max(0f, value); SetVerticesDirty(); } }

        public void SetVertices(IReadOnlyList<Vector2> source)
        {
            if (source == null) { vertices = new Vector2[0]; SetVerticesDirty(); return; }
            vertices = new Vector2[source.Count];
            for (int i = 0; i < source.Count; i++) vertices[i] = source[i];
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (vertices.Length < 3) return;
            Phase3PolygonMeshData mesh;
            try { mesh = Phase3PolygonMeshBuilder.Build(vertices); }
            catch (System.ArgumentException) { return; }
            AddPolygon(helper, mesh, color, Vector2.zero);
            if (outlineWidth > 0f)
            {
                AddPolygon(helper, mesh, outlineColor, new Vector2(outlineWidth, 0f));
                AddPolygon(helper, mesh, outlineColor, new Vector2(-outlineWidth, 0f));
                AddPolygon(helper, mesh, outlineColor, new Vector2(0f, outlineWidth));
                AddPolygon(helper, mesh, outlineColor, new Vector2(0f, -outlineWidth));
                AddPolygon(helper, mesh, color, Vector2.zero);
            }
        }

        private static void AddPolygon(VertexHelper helper, Phase3PolygonMeshData mesh, Color tint, Vector2 offset)
        {
            int start = helper.currentVertCount;
            for (int i = 0; i < mesh.Vertices.Length; i++) helper.AddVert(mesh.Vertices[i] + offset, tint, Vector2.zero);
            for (int i = 0; i < mesh.Indices.Length; i += 3) helper.AddTriangle(start + mesh.Indices[i], start + mesh.Indices[i + 1], start + mesh.Indices[i + 2]);
        }
    }
}
