using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.Phase3
{
    public sealed class Phase3BoardPresenter
    {
        private readonly Phase3RuntimeBinding binding;
        private readonly Phase3FieldCoordinateMapper mapper;
        private readonly Phase3RuntimeVisualSettings settings;
        private readonly Dictionary<string, Phase3PieceView> fieldViews = new Dictionary<string, Phase3PieceView>();
        private readonly Dictionary<string, Phase3SlotView> slotViews = new Dictionary<string, Phase3SlotView>();

        public Phase3BoardPresenter(Phase3RuntimeBinding binding, Phase3FieldCoordinateMapper mapper, Phase3RuntimeVisualSettings settings) { this.binding = binding; this.mapper = mapper; this.settings = settings; }
        public void RegisterPieceView(Phase3PieceView view) => fieldViews[view.PieceId] = view;
        public void RegisterSlotView(Phase3SlotView view) => slotViews[view.SlotId] = view;
        public Phase3PieceView GetPieceView(string id) => fieldViews.TryGetValue(id, out Phase3PieceView view) ? view : null;
        public int PieceViewCount => fieldViews.Count;
        public int SlotViewCount => slotViews.Count;

        public void EnsureViews(Phase3PuzzleSessionModel session, Phase3RuntimeOrchestrator orchestrator)
        {
            for (int i = 0; i < session.Slots.Count; i++)
            {
                Phase3SlotModel slot = session.Slots[i];
                if (slotViews.ContainsKey(slot.SlotId)) continue;
                var go = new GameObject("Phase3 Slot " + slot.SlotId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Phase3PolygonGraphic), typeof(Phase3SlotView));
                var view = go.GetComponent<Phase3SlotView>();
                view.Configure(slot, binding.TargetLayer, mapper, settings);
                RegisterSlotView(view);
            }
            for (int i = 0; i < session.Pieces.Count; i++)
            {
                Phase3PieceModel piece = session.Pieces[i];
                if (fieldViews.ContainsKey(piece.PieceId)) continue;
                var go = new GameObject("Phase3 Field Piece " + piece.PieceId, typeof(RectTransform), typeof(CanvasRenderer), typeof(Phase3PolygonGraphic), typeof(Phase3PieceInputRelay), typeof(Phase3PieceView));
                (go.transform as RectTransform).SetParent(binding.LooseLayer, false);
                var relay = go.GetComponent<Phase3PieceInputRelay>(); relay.Bind(orchestrator);
                var view = go.GetComponent<Phase3PieceView>();
                view.Configure(piece.PieceId, go.transform as RectTransform, go.GetComponent<Phase3PolygonGraphic>(), relay);
                view.SetGeometry(piece.Definition.ShapeDefinition);
                view.Graphic.OutlineColor = settings.OutlineColor; view.Graphic.OutlineWidth = settings.OutlineWidth;
                RegisterPieceView(view);
            }
        }

        public void Refresh(Phase3PuzzleSessionModel session)
        {
            for (int i = 0; i < session.Pieces.Count; i++)
            {
                Phase3PieceModel piece = session.Pieces[i];
                if (!fieldViews.TryGetValue(piece.PieceId, out Phase3PieceView view)) continue;
                if (piece.State == Phase3PieceState.InDeck) view.Present(Phase3PieceDisplayState.Hidden, piece, binding.LooseLayer, Vector2.zero, settings.DeckColor);
                else
                {
                    RectTransform layer = piece.State == Phase3PieceState.Dragging ? binding.DragLayer : piece.State == Phase3PieceState.Placed ? binding.PlacedLayer : binding.LooseLayer;
                    Vector2 position = piece.HasFieldCentroid ? mapper.CanonicalToRectLocal(piece.FieldCentroid) : Vector2.zero;
                    Phase3PieceDisplayState state = piece.State == Phase3PieceState.Dragging ? Phase3PieceDisplayState.Dragging : piece.State == Phase3PieceState.Placed ? Phase3PieceDisplayState.Placed : Phase3PieceDisplayState.Loose;
                    Color color = state == Phase3PieceDisplayState.Dragging ? settings.DraggingColor : state == Phase3PieceDisplayState.Placed ? settings.PlacedColor : settings.LooseColor;
                    view.Present(state, piece, layer, TransformLocal(binding.FieldRoot, layer, position), color);
                }
            }
            for (int i = 0; i < session.Slots.Count; i++) if (slotViews.TryGetValue(session.Slots[i].SlotId, out Phase3SlotView slotView)) slotView.Refresh(session.Slots[i]);
        }

        public void SetDraggingPosition(string pieceId, Vector2 fieldLocal)
        {
            Phase3PieceView view = GetPieceView(pieceId);
            if (view) view.RectTransform.anchoredPosition = TransformLocal(binding.FieldRoot, binding.DragLayer, fieldLocal);
        }

        public void ClearViews()
        {
            foreach (Phase3PieceView view in fieldViews.Values) Retire(view);
            foreach (Phase3SlotView view in slotViews.Values) Retire(view);
            fieldViews.Clear();
            slotViews.Clear();
        }

        private static void Retire(Component view)
        {
            if (!view) return;
            view.gameObject.SetActive(false);
            if (Application.isPlaying) UnityEngine.Object.Destroy(view.gameObject);
            else UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        public static Vector2 TransformLocal(RectTransform from, RectTransform to, Vector2 point) => to.InverseTransformPoint(from.TransformPoint(point));
    }
}
