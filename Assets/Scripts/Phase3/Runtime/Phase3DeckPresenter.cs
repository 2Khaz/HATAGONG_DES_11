using System;
using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.Phase3
{
    public sealed class Phase3DeckPresenter
    {
        private readonly Phase3RuntimeBinding binding;
        private readonly Phase3RuntimeVisualSettings settings;
        private readonly Dictionary<string, Phase3PieceView> views = new Dictionary<string, Phase3PieceView>();
        private readonly List<Phase3PieceModel> ordered = new List<Phase3PieceModel>();
        private Phase3PuzzleSessionModel boundSession;
        private bool lastInputEnabled;
        public int CurrentPage { get; private set; }
        public int ViewCount => views.Count;

        public Phase3DeckPresenter(Phase3RuntimeBinding binding, Phase3RuntimeVisualSettings settings = null)
        {
            this.binding = binding;
            this.settings = settings ?? new Phase3RuntimeVisualSettings();
            binding.PreviousPageButton.onClick.AddListener(PreviousPage);
            binding.NextPageButton.onClick.AddListener(NextPage);
        }
        public void RegisterView(Phase3PieceView view) => views[view.PieceId] = view;
        public void EnsureViews(Phase3PuzzleSessionModel session, Phase3RuntimeOrchestrator orchestrator)
        {
            for (int i = 0; i < session.Pieces.Count; i++)
            {
                Phase3PieceModel piece = session.Pieces[i];
                if (views.ContainsKey(piece.PieceId)) continue;
                var go = new GameObject("Phase3 Deck Piece " + piece.PieceId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Phase3PolygonGraphic), typeof(Phase3PieceInputRelay), typeof(Phase3PieceView));
                (go.transform as RectTransform).SetParent(binding.DeckRoot, false);
                var relay = go.GetComponent<Phase3PieceInputRelay>(); relay.Bind(orchestrator);
                var view = go.GetComponent<Phase3PieceView>();
                view.Configure(piece.PieceId, go.transform as RectTransform, go.GetComponent<Phase3PolygonGraphic>(), relay);
                view.Graphic.OutlineColor = settings.OutlineColor; view.Graphic.OutlineWidth = settings.OutlineWidth;
                RegisterView(view);
            }
        }
        public void Bind(Phase3PuzzleSessionModel session)
        {
            boundSession = session;
            ordered.Clear(); for (int i = 0; i < session.Pieces.Count; i++) ordered.Add(session.Pieces[i]);
            if (ordered.Count > Phase3RuntimeInputPolicy.DeckMaximumPieceCount) throw new ArgumentOutOfRangeException(nameof(session), "Deck supports at most eight pieces.");
            ordered.Sort((a, b) => StringComparer.Ordinal.Compare(a.OriginalDeckSlotId, b.OriginalDeckSlotId));
            for (int i = 1; i < ordered.Count; i++) if (string.Equals(ordered[i - 1].OriginalDeckSlotId, ordered[i].OriginalDeckSlotId, StringComparison.Ordinal)) throw new ArgumentException("Duplicate deck slot ID.", nameof(session));
            CurrentPage = 0;
        }
        public bool SetPage(int page) { if (page < 0 || page > 1 || page == CurrentPage) return false; CurrentPage = page; return true; }
        public void Refresh(Phase3PuzzleSessionModel session, bool inputEnabled)
        {
            boundSession = session;
            lastInputEnabled = inputEnabled;
            bool locked = !inputEnabled || session.HasActiveDrag || session.IsCleared;
            binding.PreviousPageButton.interactable = !locked && CurrentPage > 0;
            binding.NextPageButton.interactable = !locked && CurrentPage < 1 && ordered.Count > Phase3RuntimeInputPolicy.DeckPageSize;
            for (int i = 0; i < ordered.Count; i++) if (views.TryGetValue(ordered[i].PieceId, out Phase3PieceView view))
            {
                if (ordered[i].State == Phase3PieceState.Dragging)
                {
                    // EventSystem keeps this GameObject as pointerDrag after BeginDrag.
                    // Keep the relay alive while the separate field view is presented in
                    // the drag overlay, but do not leave a second visible/raycastable piece.
                    view.gameObject.SetActive(true);
                    view.Graphic.enabled = false;
                    view.Graphic.raycastTarget = false;
                    continue;
                }

                bool visible = ordered[i].State == Phase3PieceState.InDeck && i / Phase3RuntimeInputPolicy.DeckPageSize == CurrentPage;
                if (!visible) view.gameObject.SetActive(false);
                else
                {
                    RectTransform slotRoot = binding.DeckSlotRoots[i % 4];
                    float scale = CalculateIconScale(ordered[i].Definition.ShapeDefinition, slotRoot.rect, settings.DeckSlotPadding, settings.DeckIconMaximumScale);
                    view.SetGeometry(ordered[i].Definition.ShapeDefinition, scale);
                    view.Graphic.enabled = true;
                    view.Present(Phase3PieceDisplayState.Deck, ordered[i], slotRoot, Vector2.zero, settings.DeckColor);
                }
            }
        }
        public void Dispose()
        {
            if (!binding) return;
            if (binding.PreviousPageButton) binding.PreviousPageButton.onClick.RemoveListener(PreviousPage);
            if (binding.NextPageButton) binding.NextPageButton.onClick.RemoveListener(NextPage);
        }
        public void ResetPresentation()
        {
            foreach (Phase3PieceView view in views.Values)
            {
                if (!view) continue;
                view.gameObject.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(view.gameObject);
                else UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
            views.Clear();
            ordered.Clear();
            boundSession = null;
            lastInputEnabled = false;
            CurrentPage = 0;
            if (binding)
            {
                if (binding.PreviousPageButton) binding.PreviousPageButton.interactable = false;
                if (binding.NextPageButton) binding.NextPageButton.interactable = false;
            }
        }
        private void PreviousPage() { if (SetPage(CurrentPage - 1) && boundSession != null) Refresh(boundSession, lastInputEnabled); }
        private void NextPage() { if (SetPage(CurrentPage + 1) && boundSession != null) Refresh(boundSession, lastInputEnabled); }
        public static float CalculateIconScale(Phase3ShapeDefinition shape, Rect slotRect, float padding, float maximum)
        {
            float radius = 0f;
            for (int i = 0; i < shape.Vertices.Count; i++)
            {
                float x = (float)((shape.Vertices[i].X - shape.Centroid.X) * Phase3FieldCoordinateMapper.LogicalScale);
                float y = (float)((shape.Vertices[i].Y - shape.Centroid.Y) * Phase3FieldCoordinateMapper.LogicalScale);
                radius = Mathf.Max(radius, Mathf.Sqrt(x * x + y * y));
            }
            float available = Mathf.Max(0f, Mathf.Min(slotRect.width, slotRect.height) * 0.5f - padding);
            return radius > 0f ? Mathf.Min(maximum, available / radius) : 1f;
        }
    }
}
