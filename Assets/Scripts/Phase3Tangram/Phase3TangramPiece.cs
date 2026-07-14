using System;
using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.Phase3Tangram
{
    public enum TangramPieceState { InDeck, Dragging, Loose, Placed }

    public sealed class TangramTargetAssignment
    {
        public TangramTargetAssignment(int targetId, IReadOnlyList<Vector2> absolutePolygon)
        {
            TargetId = targetId;
            AbsolutePolygon = absolutePolygon ?? throw new ArgumentNullException(nameof(absolutePolygon));
            TargetPosition = Phase3TangramGenerator.GetAreaCentroid(absolutePolygon);
        }

        public int TargetId { get; }
        public IReadOnlyList<Vector2> AbsolutePolygon { get; }
        public Vector2 TargetPosition { get; }
    }

    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D))]
    public sealed class Phase3TangramPiece : MonoBehaviour
    {
        private Mesh mesh;
        private Material material;
        private MeshRenderer meshRenderer;
        private PolygonCollider2D polygonCollider;

        public int Id { get; private set; }
        public IReadOnlyList<Vector2> OriginalShape { get; private set; }
        public TangramTargetAssignment Assignment { get; private set; }
        public bool IsPlaced => State == TangramPieceState.Placed;
        public TangramPieceState State { get; private set; }
        public int OriginalDeckSlotId { get; private set; }
        public int OriginalDeckPage { get; private set; }
        public Vector3 DragOffset { get; set; }
        public Vector3 LastStableLoosePosition { get; set; }
        public bool HasStableLoosePosition { get; set; }
        public int CurrentRotationStep { get; private set; }
        public Color PieceColor { get; private set; }
        public PolygonCollider2D PolygonCollider => polygonCollider;
        public int SortingOrder => meshRenderer ? meshRenderer.sortingOrder : int.MinValue;

        public void Initialize(int id, IReadOnlyList<Vector2> originalShape, TangramTargetAssignment assignment, Color color, int deckPage, int deckSlot, int initialRotationStep)
        {
            if (originalShape == null || originalShape.Count < 3) throw new ArgumentException("Tangram piece needs at least three vertices.", nameof(originalShape));
            Id = id;
            OriginalShape = Copy(originalShape).AsReadOnly();
            Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));
            OriginalDeckPage = deckPage;
            OriginalDeckSlotId = deckSlot;
            PieceColor = color;
            State = TangramPieceState.InDeck;
            meshRenderer = GetComponent<MeshRenderer>();
            polygonCollider = GetComponent<PolygonCollider2D>();
            material = new Material(Shader.Find("Sprites/Default")) { color = color };
            meshRenderer.sharedMaterial = material;
            meshRenderer.sortingOrder = 5000 + id;
            GenerateMeshAndCollider();
            SetRotationStep(initialRotationStep);
        }

        public void GenerateMeshAndCollider()
        {
            Vector2[] colliderVertices = new Vector2[OriginalShape.Count];
            Vector3[] meshVertices = new Vector3[OriginalShape.Count];
            for (int i = 0; i < OriginalShape.Count; i++)
            {
                colliderVertices[i] = OriginalShape[i];
                meshVertices[i] = new Vector3(OriginalShape[i].x, OriginalShape[i].y, 0f);
            }
            polygonCollider.pathCount = 1;
            polygonCollider.SetPath(0, colliderVertices);
            int[] triangles = new int[(OriginalShape.Count - 2) * 3];
            for (int i = 1; i < OriginalShape.Count - 1; i++) { int index = (i - 1) * 3; triangles[index] = 0; triangles[index + 1] = i; triangles[index + 2] = i + 1; }
            if (mesh) Destroy(mesh);
            mesh = new Mesh { name = $"Phase3TangramPiece_{Id}_Mesh" };
            mesh.vertices = meshVertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        public List<Vector2> GetCurrentAbsoluteShape()
        {
            var result = new List<Vector2>(OriginalShape.Count);
            for (int i = 0; i < OriginalShape.Count; i++)
            {
                Vector3 world = transform.TransformPoint(new Vector3(OriginalShape[i].x, OriginalShape[i].y, 0f));
                result.Add(new Vector2(world.x, world.y));
            }
            return result;
        }

        public List<Vector3> GetCurrentWorldShape()
        {
            var result = new List<Vector3>(OriginalShape.Count);
            for (int i = 0; i < OriginalShape.Count; i++)
            {
                result.Add(transform.TransformPoint(new Vector3(OriginalShape[i].x, OriginalShape[i].y, 0f)));
            }
            return result;
        }

        public void SetAssignment(TangramTargetAssignment assignment) => Assignment = assignment ?? throw new ArgumentNullException(nameof(assignment));

        public void SetState(TangramPieceState state)
        {
            State = state;
            polygonCollider.enabled = state != TangramPieceState.Placed;
            if (state == TangramPieceState.Dragging) meshRenderer.sortingOrder = 7000;
            else meshRenderer.sortingOrder = 5000 + Id;
        }

        public void SetRotationStep(int step)
        {
            CurrentRotationStep = NormalizeStep(step);
            transform.localRotation = Quaternion.Euler(0f, 0f, CurrentRotationStep * 45f);
        }

        public void Rotate45(int direction) => SetRotationStep(CurrentRotationStep + (direction >= 0 ? 1 : -1));
        public void SetSortingOrder(int value) { if (meshRenderer) meshRenderer.sortingOrder = value; }
        public void SetVisible(bool visible) { if (meshRenderer) meshRenderer.enabled = visible; if (polygonCollider) polygonCollider.enabled = visible && State != TangramPieceState.Placed; }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }

        private static int NormalizeStep(int value) => ((value % 8) + 8) % 8;
        private static List<Vector2> Copy(IReadOnlyList<Vector2> source) { var result = new List<Vector2>(source.Count); for (int i = 0; i < source.Count; i++) result.Add(source[i]); return result; }
    }
}
