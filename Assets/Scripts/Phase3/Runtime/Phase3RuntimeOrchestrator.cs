using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace HATAGONG.Phase3
{
    public sealed class Phase3RuntimeOrchestrator : MonoBehaviour
    {
        [SerializeField] private Phase3RuntimeBinding binding;
        [SerializeField] private Phase3RuntimeVisualSettings visualSettings = new Phase3RuntimeVisualSettings();
        private Phase3PuzzleSessionModel session;
        private Phase3BoardPresenter board;
        private Phase3DeckPresenter deck;
        private Phase3MobileRotateButtonPresenter mobileButton;
        private Phase3PointerDragState pointer;
        private bool inputEnabled;
        private bool clearEventSent;

        public event Action<Phase3PlayResult> OperationResolved;
        public event Action PhaseCleared;
        public event Action<Phase3PuzzleSessionModel> SessionChanged;
        public Phase3FieldCoordinateMapper Mapper { get; private set; }
        public bool InputEnabled => inputEnabled;
        public bool SessionCleared => session != null && session.IsCleared;
        public bool HasPrimaryTouchDrag => pointer != null && pointer.DeviceKind == Phase3PointerDeviceKind.Touch;
        public int ActivePointerId => pointer != null ? pointer.PointerId : int.MinValue;
        public int ActiveDeviceId => pointer != null ? pointer.DeviceId : 0;
        public int ActiveTouchId => pointer != null ? pointer.TouchId : 0;
        public bool CanRotateActiveDrag => inputEnabled && session != null && session.HasActiveDrag && !session.IsCleared && pointer != null;
        public Phase3PointerDragState ActivePointer => pointer;
        public Phase3PuzzleSessionModel Session => session;
        public Phase3BoardPresenter BoardPresenter => board;
        public Phase3DeckPresenter DeckPresenter => deck;

        public void Configure(Phase3RuntimeBinding value, Phase3BoardPresenter boardPresenter = null, Phase3DeckPresenter deckPresenter = null)
        {
            binding = value ? value : throw new ArgumentNullException(nameof(value)); binding.ValidateOrThrow();
            Mapper = new Phase3FieldCoordinateMapper(binding.FieldRoot);
            board = boardPresenter ?? new Phase3BoardPresenter(binding, Mapper, visualSettings);
            deck = deckPresenter ?? new Phase3DeckPresenter(binding, visualSettings);
        }

        public void BindSession(Phase3PuzzleSessionModel value)
        {
            if (session != null) UnbindSession();
            session = value ?? throw new ArgumentNullException(nameof(value));
            clearEventSent = session.IsCleared;
            binding.MobileInputController.Bind(this, binding);
            binding.EmptyFieldSurface.Bind(binding.MobileInputController);
            mobileButton = new Phase3MobileRotateButtonPresenter(binding.MobileRotateButton, this);
            board.EnsureViews(session, this);
            deck.EnsureViews(session, this);
            deck.Bind(session);
            inputEnabled = false;
            RefreshAllViews();
            SessionChanged?.Invoke(session);
        }
        public void UnbindSession()
        {
            bool hadSession = session != null;
            if (session != null && session.HasActiveDrag) CancelActiveDrag();
            pointer = null;
            if (binding)
            {
                binding.MobileInputController.CancelPendingSecondaryTap();
                binding.EmptyFieldSurface.Unbind();
                binding.MobileInputController.Unbind();
            }
            mobileButton?.Dispose();
            mobileButton = null;
            if (binding)
            {
                binding.MobileRotateButton.interactable = false;
                binding.MobileRotateButton.gameObject.SetActive(false);
            }
            board?.ClearViews();
            deck?.ResetPresentation();
            session = null;
            inputEnabled = false;
            clearEventSent = false;
            if (hadSession) SessionChanged?.Invoke(null);
        }
        public void SetInputEnabled(bool enabled)
        {
            if (!enabled)
            {
                binding.MobileInputController.CancelPendingSecondaryTap();
                CancelActiveDrag();
            }
            inputEnabled = enabled && session != null && !session.IsCleared;
            RefreshAllViews();
        }

        public bool TryCalculatePressAnchor(string pieceId, RectTransform pressedView, PointerEventData data, out Vector2 anchorCanonical)
        {
            anchorCanonical = default;
            if (!inputEnabled || session == null || session.IsCleared || session.HasActiveDrag) return false;
            Phase3PieceModel piece = session.GetPiece(pieceId);
            if (piece == null || piece.State == Phase3PieceState.Placed || !pressedView) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(pressedView, data.position, data.pressEventCamera, out Vector2 local)) return false;
            Phase3PieceView pressedPieceView = pressedView.GetComponent<Phase3PieceView>();
            float scale = pressedPieceView != null && pressedPieceView.GeometryScale > Mathf.Epsilon ? pressedPieceView.GeometryScale : 1f;
            anchorCanonical = local / scale;
            return true;
        }

        public bool TryBeginPointerDrag(string pieceId, PointerEventData data, Vector2 anchorCanonical, Phase3PointerDeviceKind kind, int touchId)
        {
            return TryBeginPointerDrag(pieceId, data, anchorCanonical, kind, touchId, 0);
        }

        public bool TryBeginPointerDrag(string pieceId, PointerEventData data, Vector2 anchorCanonical, Phase3PointerDeviceKind kind, int touchId, int deviceId)
        {
            if (!inputEnabled || session == null || pointer != null || data.button != PointerEventData.InputButton.Left || kind == Phase3PointerDeviceKind.Unknown) return false;
            Phase3PlayResult result = session.BeginDrag(pieceId); Publish(result); if (!result.IsSuccess) return false;
            Phase3PieceModel piece = session.GetPiece(pieceId);
            pointer = new Phase3PointerDragState { PieceId = pieceId, PointerId = data.pointerId, DeviceKind = kind, DeviceId = deviceId, TouchId = touchId, AnchorCanonical = anchorCanonical, ScreenPosition = data.position, EventCamera = data.pressEventCamera, RotationAtBegin = piece.CurrentRotation, StateAtBegin = session.ActiveDrag.OriginState, UsesMobileLift = kind == Phase3PointerDeviceKind.Touch };
            RefreshAllViews();
            return ApplyActiveDragPose();
        }

        public bool UpdatePointerDrag(int pointerId, Vector2 screenPosition, Camera camera)
        {
            if (pointer == null || pointer.PointerId != pointerId) return false;
            pointer.ScreenPosition = screenPosition;
            pointer.EventCamera = camera;
            return ApplyActiveDragPose();
        }

        public bool ApplyActiveDragPose()
        {
            if (pointer == null || session == null || !session.HasActiveDrag || !string.Equals(session.ActivePieceId, pointer.PieceId, StringComparison.Ordinal)) return false;
            if (!Mapper.TryScreenToRectLocal(pointer.ScreenPosition, pointer.EventCamera, out Vector2 fieldLocal)) return false;
            pointer.FieldLocalPosition = fieldLocal; pointer.CanonicalPointer = Mapper.RectLocalToCanonical(fieldLocal);
            Phase3PieceModel piece = session.GetPiece(pointer.PieceId);
            if (piece == null) return false;
            Vector2 rotatedAnchor = Phase3FieldCoordinateMapper.RotateClockwise(pointer.AnchorCanonical, piece.CurrentRotation);
            Vector2 centroid = fieldLocal - rotatedAnchor + (pointer.UsesMobileLift ? Vector2.up * Phase3RuntimeInputPolicy.MobileDragLift : Vector2.zero);
            board.SetDraggingPosition(pointer.PieceId, centroid); return true;
        }

        public bool EndPointerDrag(int pointerId)
        {
            if (pointer == null || pointer.PointerId != pointerId || pointer.DropHandled) return false;
            if (pointer.DeviceKind == Phase3PointerDeviceKind.Mouse && Mouse.current != null && Mouse.current.leftButton.isPressed) return false;
            pointer.DropHandled = true;
            Vector2 displayedCentroid = DisplayedCentroidLocal();
            Vector3 world = binding.FieldRoot.TransformPoint(displayedCentroid);
            Phase3DropIntent intent = Phase3DropIntentResolver.Resolve(world, binding.DeckRoot, binding.FieldRoot);
            Phase3PlayResult result = intent == Phase3DropIntent.Deck ? session.ReturnActiveToDeck(pointer.PieceId) : intent == Phase3DropIntent.Field ? session.DropActiveOnField(pointer.PieceId, Mapper.RectLocalToCanonical(displayedCentroid)) : session.CancelActiveDrag();
            Publish(result);
            if (session.HasActiveDrag)
            {
                Phase3PlayResult recovery = session.CancelActiveDrag();
                Publish(recovery);
            }
            pointer = null;
            binding.MobileInputController.CancelPendingSecondaryTap();
            RefreshAllViews();
            return true;
        }

        public Phase3PlayResult CancelActiveDrag()
        {
            if (session == null || !session.HasActiveDrag) { pointer = null; return default; }
            Phase3PlayResult result = session.CancelActiveDrag();
            pointer = null;
            binding.MobileInputController.CancelPendingSecondaryTap();
            Publish(result);
            RefreshAllViews();
            return result;
        }
        public bool TryRotateClockwise() => Rotate(true);
        public bool TryRotateCounterClockwise() => Rotate(false);
        private bool Rotate(bool clockwise)
        {
            if (!CanRotateActiveDrag) return false;
            Phase3PlayResult result = clockwise ? session.RotateActiveClockwise(pointer.PieceId) : session.RotateActiveCounterClockwise(pointer.PieceId);
            Publish(result);
            RefreshAllViews();
            return result.IsSuccess;
        }
        private Vector2 DisplayedCentroidLocal()
        {
            Phase3PieceModel piece = session.GetPiece(pointer.PieceId);
            return pointer.FieldLocalPosition - Phase3FieldCoordinateMapper.RotateClockwise(pointer.AnchorCanonical, piece.CurrentRotation) + (pointer.UsesMobileLift ? Vector2.up * Phase3RuntimeInputPolicy.MobileDragLift : Vector2.zero);
        }
        public void RefreshAllViews(bool? mobilePlatformOverride = null)
        {
            if (session != null) { board?.Refresh(session); deck?.Refresh(session, inputEnabled); }
            if (pointer != null) ApplyActiveDragPose();
            mobileButton?.Refresh(mobilePlatformOverride ?? Application.isMobilePlatform);
        }
        private void Publish(Phase3PlayResult result)
        {
            OperationResolved?.Invoke(result);
            if (result.PhaseCleared && !clearEventSent)
            {
                clearEventSent = true;
                inputEnabled = false;
                binding.MobileInputController.CancelPendingSecondaryTap();
                PhaseCleared?.Invoke();
            }
        }
        public void HandleApplicationFocus(bool focused) { if (!focused) SetInputEnabled(false); }
        public void HandleApplicationPause(bool paused) { if (paused) SetInputEnabled(false); }
        private void OnApplicationFocus(bool focused) => HandleApplicationFocus(focused);
        private void OnApplicationPause(bool paused) => HandleApplicationPause(paused);
        private void OnDestroy()
        {
            if (binding) UnbindSession();
            else { pointer = null; session = null; board?.ClearViews(); }
            deck?.Dispose();
        }
    }
}
