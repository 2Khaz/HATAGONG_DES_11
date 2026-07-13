using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HATAGONG.Phase3
{
    public sealed class Phase3RuntimeBinding : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private RectTransform fieldRoot;
        [SerializeField] private Phase3EmptyFieldSurface emptyFieldSurface;
        [SerializeField] private RectTransform targetLayer;
        [SerializeField] private RectTransform placedLayer;
        [SerializeField] private RectTransform looseLayer;
        [SerializeField] private RectTransform dragLayer;
        [SerializeField] private RectTransform deckRoot;
        [SerializeField] private RectTransform[] deckSlotRoots = new RectTransform[4];
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Button mobileRotateButton;
        [SerializeField] private Phase3MobileInputController mobileInputController;

        public Canvas Canvas => canvas;
        public GraphicRaycaster GraphicRaycaster => graphicRaycaster;
        public EventSystem EventSystem => eventSystem;
        public RectTransform FieldRoot => fieldRoot;
        public Phase3EmptyFieldSurface EmptyFieldSurface => emptyFieldSurface;
        public RectTransform TargetLayer => targetLayer;
        public RectTransform PlacedLayer => placedLayer;
        public RectTransform LooseLayer => looseLayer;
        public RectTransform DragLayer => dragLayer;
        public RectTransform DeckRoot => deckRoot;
        public RectTransform[] DeckSlotRoots => deckSlotRoots;
        public Button PreviousPageButton => previousPageButton;
        public Button NextPageButton => nextPageButton;
        public Button MobileRotateButton => mobileRotateButton;
        public Phase3MobileInputController MobileInputController => mobileInputController;

        public void Configure(Canvas valueCanvas, GraphicRaycaster raycaster, EventSystem valueEventSystem, RectTransform valueField,
            Phase3EmptyFieldSurface emptySurface, RectTransform targets, RectTransform placed, RectTransform loose, RectTransform drag,
            RectTransform deck, RectTransform[] slots, Button previous, Button next, Button rotate,
            Phase3MobileInputController mobileInput = null)
        {
            canvas = valueCanvas; graphicRaycaster = raycaster; eventSystem = valueEventSystem; fieldRoot = valueField;
            emptyFieldSurface = emptySurface; targetLayer = targets; placedLayer = placed; looseLayer = loose; dragLayer = drag;
            deckRoot = deck; deckSlotRoots = slots; previousPageButton = previous; nextPageButton = next; mobileRotateButton = rotate;
            mobileInputController = mobileInput;
        }

        public void ValidateOrThrow()
        {
            if (!canvas || !graphicRaycaster || !eventSystem || !fieldRoot || !emptyFieldSurface || !targetLayer || !placedLayer || !looseLayer || !dragLayer || !deckRoot || !previousPageButton || !nextPageButton || !mobileRotateButton || !mobileInputController)
                throw new InvalidOperationException("Phase 3 runtime binding has missing required references.");
            if (!eventSystem.GetComponent<InputSystemUIInputModule>())
                throw new InvalidOperationException("EventSystem must use InputSystemUIInputModule.");
            Phase3FieldCoordinateMapper.ValidateFieldRect(fieldRoot.rect);
            if (deckSlotRoots == null || deckSlotRoots.Length != Phase3RuntimeInputPolicy.DeckPageSize) throw new InvalidOperationException("Exactly four deck slot roots are required.");
            for (int i = 0; i < deckSlotRoots.Length; i++) if (!deckSlotRoots[i]) throw new InvalidOperationException("Deck slot roots cannot contain null.");
            RectTransform[] layers = { targetLayer, placedLayer, looseLayer };
            for (int i = 0; i < layers.Length; i++)
            {
                if (!layers[i].IsChildOf(fieldRoot)) throw new InvalidOperationException("All field layers must be children of FieldRoot.");
                if (Quaternion.Angle(layers[i].localRotation, Quaternion.identity) > 0.01f || (layers[i].localScale - Vector3.one).sqrMagnitude > 0.0001f) throw new InvalidOperationException("Field layers require identity rotation and unit scale.");
                for (int j = i + 1; j < layers.Length; j++) if (layers[i] == layers[j]) throw new InvalidOperationException("Field layers must be distinct objects.");
            }
            if (!(targetLayer.GetSiblingIndex() < placedLayer.GetSiblingIndex() && placedLayer.GetSiblingIndex() < looseLayer.GetSiblingIndex()))
                throw new InvalidOperationException("Field layer sibling order must be Target, Placed, Loose.");
            if (dragLayer == fieldRoot || dragLayer.IsChildOf(fieldRoot) || dragLayer.GetComponentInParent<Canvas>() != canvas ||
                Quaternion.Angle(dragLayer.localRotation, Quaternion.identity) > 0.01f || (dragLayer.localScale - Vector3.one).sqrMagnitude > 0.0001f)
                throw new InvalidOperationException("DragLayer must be an identity overlay under the bound Canvas and outside FieldRoot.");
            if (!emptyFieldSurface.transform.IsChildOf(fieldRoot))
                throw new InvalidOperationException("EmptyFieldSurface must be inside FieldRoot.");
            Graphic emptyGraphic = emptyFieldSurface.GetComponent<Graphic>();
            if (!emptyGraphic || !emptyGraphic.raycastTarget)
                throw new InvalidOperationException("EmptyFieldSurface requires a raycast-enabled Graphic on the marker GameObject.");
            Graphic[] targetGraphics = targetLayer.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < targetGraphics.Length; i++)
                if (targetGraphics[i].raycastTarget) throw new InvalidOperationException("Target slot graphics must not receive raycasts.");
        }
    }
}
