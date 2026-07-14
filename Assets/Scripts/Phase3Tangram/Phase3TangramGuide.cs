using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.Phase3Tangram
{
    public sealed class Phase3TangramGuide
    {
        private readonly Transform root;
        private readonly Material material;
        private readonly List<LineRenderer> lines = new List<LineRenderer>();

        public Phase3TangramGuide(Transform parent)
        {
            var rootObject = new GameObject("Phase3 Tangram Closed Guides");
            root = rootObject.transform;
            root.SetParent(parent, false);
            material = new Material(Shader.Find("Sprites/Default")) { color = new Color(0.35f, 0.88f, 1f, 0.95f) };
        }

        public int PolygonCount => lines.Count;
        public GameObject RootObject => root.gameObject;

        public void Build(IReadOnlyList<TangramTargetAssignment> targets, Phase3TangramManager manager)
        {
            ClearLines();
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                TangramTargetAssignment target = targets[targetIndex];
                var go = new GameObject($"TargetGuide_{target.TargetId}");
                go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.loop = true;
                line.sharedMaterial = material;
                line.startColor = line.endColor = material.color;
                line.widthMultiplier = manager.GuideWorldWidth;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.sortingOrder = 4900;
                line.positionCount = target.AbsolutePolygon.Count;
                for (int vertex = 0; vertex < target.AbsolutePolygon.Count; vertex++)
                {
                    line.SetPosition(vertex, manager.LogicalToGuideWorld(target.AbsolutePolygon[vertex]));
                }
                lines.Add(line);
            }
        }

        public void SetVisible(bool value) => root.gameObject.SetActive(value);

        public void Dispose()
        {
            ClearLines();
            if (material) Object.Destroy(material);
            if (root) Object.Destroy(root.gameObject);
        }

        private void ClearLines()
        {
            for (int i = 0; i < lines.Count; i++) if (lines[i]) Object.Destroy(lines[i].gameObject);
            lines.Clear();
        }
    }
}
