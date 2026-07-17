using System;
using System.Collections;
using System.Collections.Generic;
using HATAGONG.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace HATAGONG.Phase3Tangram
{
    public sealed class Phase3TangramManager : MonoBehaviour, IGamePhase
    {
        private static readonly Color[] PieceColors =
        {
            new Color(1f, 0.31f, 0.28f), new Color(0.12f, 0.72f, 0.92f), new Color(1f, 0.78f, 0.12f), new Color(0.24f, 0.86f, 0.45f),
            new Color(0.67f, 0.43f, 0.96f), new Color(1f, 0.47f, 0.13f), new Color(0.18f, 0.82f, 0.76f), new Color(0.94f, 0.25f, 0.57f)
        };

        [SerializeField] private Canvas canvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private GameScoreController scoreController;
        [SerializeField] private Image phaseBackground;
        [SerializeField] private RectTransform deckHost;
        [SerializeField] private Image deckPanel;
        [SerializeField] private Sprite deckSprite;
        [SerializeField] private Sprite deckNavigationButtonSprite;
        [SerializeField] private Sprite completionSprite;
        [SerializeField] private Material completionShineMaterial;
        [SerializeField] private bool useFixedSeedForDebug;
        [FormerlySerializedAs("requestedSeed")]
        [SerializeField] private long fixedSeed = 260714;

        private readonly List<Phase3TangramPiece> pieces = new List<Phase3TangramPiece>();
        private readonly List<TangramTargetAssignment> targets = new List<TangramTargetAssignment>();
        private RectTransform field;
        private RectTransform deck;
        private RectTransform[] deckSlots;
        private Button previousButton;
        private Button nextButton;
        private Text pageLabel;
        private Image completionImage;
        private Image completionShineImage;
        private Material completionShineRuntimeMaterial;
        private Transform worldRoot;
        private Phase3TangramGuide guide;
        private Phase3TangramPiece selectedPiece;
        private Phase3TangramPiece draggingPiece;
        private TangramPieceState dragOriginState;
        private int page;
        private int looseSortingSequence;
        private bool built;
        private bool inputRequested;
        private bool focusSuspended;
        private bool pauseSuspended;
        private bool canvasAdjusted;
        private RenderMode originalRenderMode;
        private Camera originalCanvasCamera;
        private float originalPlaneDistance;
        private GameDifficulty difficulty;
        private long activeSeed;
        private Vector3 boardOriginWorld;
        private Vector3 boardUnitXWorld;
        private Vector3 boardUnitYWorld;
        private bool boardFrameReady;
        private Sprite deckFrame1;
        private Sprite deckFrame2;
        private Sprite deckFrame3;
        private Coroutine deckAnimation;
        private Coroutine completionShineAnimation;
        private Func<GamePhaseId, bool> terminalClearCommit;
        private bool terminalClearCommitted;
        private bool completionPresentationFinishing;
        private bool destroying;

        private const float CompletionShineDuration = 0.75f;
        private const float CompletionShineWidth = 0.22f;
        public const int CompletionShineSortingOrder = 9000;

        public GamePhaseId PhaseId => GamePhaseId.Phase3;
        public bool IsPrepared { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsExitReady { get; private set; }
        public float GuideWorldWidth => Mathf.Max(0.003f, BoardWorldSide * 0.004f);
        public int PieceCount => pieces.Count;
        public int GuideCount => guide?.PolygonCount ?? 0;
        public long ActiveSeed => activeSeed;
        public GameRunContext RunContext { get; private set; }
        public event Action PhaseCleared;
        public event Action PhaseExitReady;

        public void BindTerminalClearCommit(Func<GamePhaseId, bool> commit) => terminalClearCommit = commit;

        public bool Prepare(GameRunContext context)
        {
            if (IsCleared && terminalClearCommitted && !IsExitReady) FinishCompletionPresentation();
            SetInputEnabled(false);
            if (phaseBackground) phaseBackground.gameObject.SetActive(false);
            IsPrepared = IsRunning = IsCleared = IsExitReady = false;
            terminalClearCommitted = false;
            if (!context.IsValid || !canvas || !graphicRaycaster || !eventSystem || !worldCamera || !scoreController || !phaseBackground || !deckHost || !deckPanel || !deckSprite || !EnsureDeckFrames()) return false;
            try
            {
                EnsureBuilt();
                activeSeed = context.HasSelectedRequest ? context.Phase3Seed : (useFixedSeedForDebug ? fixedSeed : CreateRandomSeed());
                TangramGenerationResult generated = Phase3TangramGenerator.Generate(context.Difficulty, activeSeed);
                if (!generated.Success) { Debug.LogError($"[Phase3Tangram] Generation failed: {generated.FailureReason}", this); return false; }
                difficulty = context.Difficulty;
                int expectedCount = Phase3TangramGenerator.PieceCount(difficulty);
                if (generated.Pieces.Count != expectedCount) throw new InvalidOperationException($"Generated piece count mismatch: expected={expectedCount}, actual={generated.Pieces.Count}.");
                BuildPuzzle(generated);
                RunContext = context;
                IsPrepared = true;
                SetRuntimeVisible(false);
                gameObject.SetActive(false);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Phase3Tangram] Prepare failed: " + exception.Message, this);
                return false;
            }
        }

        public bool Activate()
        {
            if (!IsPrepared || IsCleared || !built) return false;
            gameObject.SetActive(true);
            AdjustCanvasForWorldPieces();
            SetRuntimeVisible(true);
            IsRunning = true;
            Canvas.ForceUpdateCanvases();
            RefreshLayout();
            PlayDeckOpeningAnimation();
            ApplyInputGate();
            Debug.Log($"[Phase3Tangram][Bind] difficulty={difficulty},seed={activeSeed},fixedSeed={useFixedSeedForDebug},pieces={pieces.Count},expected={Phase3TangramGenerator.PieceCount(difficulty)},visuals={pieces.Count},guides={GuideCount},deckPages={(pieces.Count + 3) / 4},boardWidth={boardUnitXWorld.magnitude * Phase3TangramGenerator.BoardSize:F5},boardHeight={boardUnitYWorld.magnitude * Phase3TangramGenerator.BoardSize:F5},meshColliderSharedShape=true,uvPieces=0,legacyRuntime=0", this);
            return true;
        }

        public void Deactivate()
        {
            if (IsCleared && terminalClearCommitted && !IsExitReady) FinishCompletionPresentation();
            SetInputEnabled(false);
            IsRunning = false;
            selectedPiece = draggingPiece = null;
            StopDeckOpeningAnimation();
            StopCompletionShine();
            SetRuntimeVisible(false);
            RestoreCanvas();
            gameObject.SetActive(false);
        }

        public void SetInputEnabled(bool enabled) { inputRequested = enabled; ApplyInputGate(); }

        public Vector3 LogicalToWorld(Vector2 logical)
        {
            if (!boardFrameReady) RefreshBoardFrame();
            return boardOriginWorld + boardUnitXWorld * logical.x + boardUnitYWorld * logical.y;
        }

        public Vector3 LogicalToGuideWorld(Vector2 logical)
        {
            return LogicalToWorld(logical) - worldCamera.transform.forward * Mathf.Max(0.002f, BoardWorldSide * 0.001f);
        }

        public void SelectPieceAt(Vector2 screenPosition)
        {
            selectedPiece = null;
            if (!InputAllowed || !TryScreenToWorld(screenPosition, out Vector3 world)) return;
            int highestOrder = int.MinValue;
            for (int i = 0; i < pieces.Count; i++)
            {
                Phase3TangramPiece piece = pieces[i];
                if (piece.State == TangramPieceState.Placed || !piece.PolygonCollider.enabled || !PointInPolygon(WorldToPlane(world), PiecePlaneShape(piece))) continue;
                if (piece.SortingOrder < highestOrder) continue;
                highestOrder = piece.SortingOrder;
                selectedPiece = piece;
            }
            if (selectedPiece != null && selectedPiece.State == TangramPieceState.Loose) selectedPiece.SetSortingOrder(6000 + ++looseSortingSequence);
        }

        public void BeginSelectedDrag(Vector2 screenPosition)
        {
            if (!InputAllowed || !selectedPiece || draggingPiece || !TryScreenToWorld(screenPosition, out Vector3 pointer)) return;
            draggingPiece = selectedPiece;
            dragOriginState = draggingPiece.State;
            draggingPiece.transform.localScale = Vector3.one * FieldWorldScale;
            if (dragOriginState == TangramPieceState.InDeck)
            {
                draggingPiece.transform.position = pointer;
            }
            else
            {
                Vector3 localGrabPoint = draggingPiece.transform.InverseTransformPoint(pointer);
                draggingPiece.transform.position += pointer - draggingPiece.transform.TransformPoint(localGrabPoint);
            }
            draggingPiece.transform.position -= worldCamera.transform.forward * DragDisplayOffset;
            draggingPiece.DragOffset = draggingPiece.transform.position - pointer;
            draggingPiece.SetState(TangramPieceState.Dragging);
            if (IsFinalUnplacedPiece(draggingPiece)) draggingPiece.SetSortingOrder(Phase3TangramPiece.FinalPieceDraggingSortingOrder);
            DragSelected(screenPosition);
        }

        public void DragSelected(Vector2 screenPosition)
        {
            if (!InputAllowed || !draggingPiece || !TryScreenToWorld(screenPosition, out Vector3 pointer)) return;
            draggingPiece.transform.position = pointer + draggingPiece.DragOffset;
        }

        public void RotateActive(int direction)
        {
            if (!InputAllowed || !draggingPiece) return;
            draggingPiece.Rotate45(direction);
        }

        public void EndSelectedDrag(Vector2 screenPosition)
        {
            if (!draggingPiece) { selectedPiece = null; return; }
            Phase3TangramPiece piece = draggingPiece;
            draggingPiece = null;
            selectedPiece = null;
            piece.transform.position = ProjectOntoBoardPlane(piece.transform.position);
            if (RectTransformUtility.RectangleContainsScreenPoint(deck, screenPosition, EventCamera)) { ReturnToDeck(piece); return; }
            if (!RectTransformUtility.RectangleContainsScreenPoint(field, screenPosition, EventCamera))
            {
                if (dragOriginState == TangramPieceState.Loose) PlaceLooseInsideField(piece);
                else CancelDrop(piece);
                return;
            }
            if (TryInterchangeableSnap(piece)) { RefreshVisibility(); return; }
            PlaceLooseInsideField(piece);
        }

        private bool InputAllowed => inputRequested && !focusSuspended && !pauseSuspended && IsPrepared && IsRunning && !IsCleared && !IsExitReady;
        private Camera EventCamera => canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        private float FieldWorldScale => (boardUnitXWorld.magnitude + boardUnitYWorld.magnitude) * 0.5f;
        private float BoardWorldSide => FieldWorldScale * Phase3TangramGenerator.BoardSize;
        private Vector3 BoardPlanePoint => worldCamera.transform.position + worldCamera.transform.forward * 10f;
        private float DragDisplayOffset => Mathf.Max(0.01f, BoardWorldSide * 0.01f);

        private void EnsureBuilt()
        {
            if (built) return;
            if (transform.childCount != 0) throw new InvalidOperationException("Phase3Root must be empty before Tangram runtime construction.");
            field = CreateRect("Phase3 Tangram Field", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1250f, 1250f));
            AddInputSurface(CreateStretch("Tangram Field Input", field));
            RectTransform completionRoot = CreateStretch("Tangram Completion Root", field);
            Canvas completionCanvas = completionRoot.gameObject.AddComponent<Canvas>();
            completionCanvas.overrideSorting = true;
            completionCanvas.sortingOrder = CompletionShineSortingOrder;
            RectTransform completion = CreateStretch("Tangram Completion Image", completionRoot);
            completionImage = completion.gameObject.AddComponent<Image>();
            completionImage.sprite = completionSprite;
            completionImage.color = Color.white;
            completionImage.raycastTarget = false;
            completionImage.preserveAspect = false;
            RectTransform shine = CreateStretch("Tangram Completion Shine", completionRoot);
            completionShineImage = shine.gameObject.AddComponent<Image>();
            completionShineImage.sprite = completionSprite;
            completionShineImage.color = Color.white;
            completionShineImage.raycastTarget = false;
            completionShineImage.preserveAspect = false;
            if (completionShineMaterial && completionShineMaterial.shader && completionShineMaterial.shader.isSupported)
            {
                try
                {
                    completionShineRuntimeMaterial = new Material(completionShineMaterial) { name = "Phase3CompletionShine (Runtime)" };
                    completionShineRuntimeMaterial.SetFloat("_Progress", -CompletionShineWidth);
                    completionShineRuntimeMaterial.SetFloat("_ShineWidth", CompletionShineWidth);
                    completionShineImage.material = completionShineRuntimeMaterial;
                }
                catch (Exception exception)
                {
                    completionShineRuntimeMaterial = null;
                    Debug.LogWarning("[Phase3Tangram] Completion shine material unavailable: " + exception.Message, this);
                }
            }
            completionRoot.gameObject.SetActive(false);
            deck = CreateStretch("Phase3 Tangram Deck", deckHost);
            Image deckBackground = deck.gameObject.AddComponent<Image>();
            deckBackground.color = Color.clear;
            deckBackground.raycastTarget = false;
            deckSlots = new RectTransform[4];
            float[] x = { -480f, -160f, 160f, 480f };
            for (int i = 0; i < deckSlots.Length; i++)
            {
                deckSlots[i] = CreateRect($"TangramDeckSlot{i + 1}", deck, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x[i], -2f), new Vector2(250f, 190f));
                AddInputSurface(deckSlots[i]);
            }
            previousButton = CreateDeckNavigationButton("TangramPrevious", deck, new Vector2(0f, 0.5f), new Vector2(48.5f, 0f), deckNavigationButtonSprite, true);
            nextButton = CreateDeckNavigationButton("TangramNext", deck, new Vector2(1f, 0.5f), new Vector2(-48.5f, 0f), deckNavigationButtonSprite, false);
            previousButton.onClick.AddListener(() => SetPage(page - 1));
            nextButton.onClick.AddListener(() => SetPage(page + 1));
            RectTransform pageLabelRect = CreateRect("TangramPageCount", deck, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -17f), new Vector2(260f, 30f));
            pageLabel = pageLabelRect.gameObject.AddComponent<Text>();
            pageLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            pageLabel.fontSize = 24;
            pageLabel.alignment = TextAnchor.MiddleCenter;
            pageLabel.color = new Color(0.12f, 0.18f, 0.28f, 0.95f);
            pageLabel.raycastTarget = false;
            var worldObject = new GameObject("Phase3 Tangram World Runtime");
            worldRoot = worldObject.transform;
            worldRoot.rotation = worldCamera.transform.rotation;
            guide = new Phase3TangramGuide(worldRoot);
            built = true;
        }

        private void BuildPuzzle(TangramGenerationResult generated)
        {
            ClearPieces();
            targets.Clear();
            for (int i = 0; i < generated.Pieces.Count; i++) targets.Add(new TangramTargetAssignment(generated.Pieces[i].Id, generated.Pieces[i].AbsolutePolygon));
            for (int i = 0; i < generated.Pieces.Count; i++)
            {
                TangramGeneratedPiece generatedPiece = generated.Pieces[i];
                TangramTargetAssignment assignment = targets[i];
                Vector2 center = assignment.TargetPosition;
                var originalShape = new List<Vector2>(generatedPiece.AbsolutePolygon.Count);
                for (int vertex = 0; vertex < generatedPiece.AbsolutePolygon.Count; vertex++) originalShape.Add(generatedPiece.AbsolutePolygon[vertex] - center);
                var go = new GameObject($"TangramPiece_{generatedPiece.Id}", typeof(MeshFilter), typeof(MeshRenderer), typeof(PolygonCollider2D), typeof(Phase3TangramPiece));
                go.transform.SetParent(worldRoot, false);
                Phase3TangramPiece piece = go.GetComponent<Phase3TangramPiece>();
                piece.Initialize(generatedPiece.Id, originalShape, assignment, PieceColors[i % PieceColors.Length], i / 4, i % 4, generatedPiece.InitialRotationStep);
                pieces.Add(piece);
            }
            page = 0;
            StopCompletionShine();
            completionImage.transform.parent.gameObject.SetActive(false);
            RefreshLayout();
        }

        private void RefreshLayout()
        {
            if (!built || pieces.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            RefreshBoardFrame();
            guide.Build(targets, this);
            guide.SetVisible(!IsCleared);
            for (int i = 0; i < pieces.Count; i++) if (pieces[i].State == TangramPieceState.InDeck) PlaceAtOriginalDeckSlot(pieces[i]);
            RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (!built || previousButton == null || nextButton == null) return;
            int pageCount = (pieces.Count + 3) / 4;
            for (int i = 0; i < pieces.Count; i++)
            {
                Phase3TangramPiece piece = pieces[i];
                if (!piece) continue;
                bool visible = piece.State != TangramPieceState.InDeck || piece.OriginalDeckPage == page;
                piece.SetVisible(visible && !IsCleared);
            }
            previousButton.interactable = InputAllowed && page > 0;
            nextButton.interactable = InputAllowed && page + 1 < pageCount;
            if (pageLabel) pageLabel.text = pageCount > 0 ? $"{page + 1} / {pageCount}  ·  {pieces.Count}" : "0 / 0";
        }

        private void PlaceAtOriginalDeckSlot(Phase3TangramPiece piece)
        {
            RectTransform slot = deckSlots[piece.OriginalDeckSlotId];
            piece.transform.position = RectLocalToWorld(slot, slot.rect.center);
            piece.transform.localScale = Vector3.one * PreviewWorldScale(piece, slot);
        }

        private float PreviewWorldScale(Phase3TangramPiece piece, RectTransform slot)
        {
            RotatedBounds(piece.OriginalShape, piece.CurrentRotationStep, out Vector2 min, out Vector2 max);
            Vector3 left = RectLocalToWorld(slot, new Vector2(slot.rect.xMin + 14f, slot.rect.center.y));
            Vector3 right = RectLocalToWorld(slot, new Vector2(slot.rect.xMax - 14f, slot.rect.center.y));
            Vector3 bottom = RectLocalToWorld(slot, new Vector2(slot.rect.center.x, slot.rect.yMin + 14f));
            Vector3 top = RectLocalToWorld(slot, new Vector2(slot.rect.center.x, slot.rect.yMax - 14f));
            float width = Mathf.Max(0.001f, max.x - min.x), height = Mathf.Max(0.001f, max.y - min.y);
            return Mathf.Min(Vector3.Distance(left, right) / width, Vector3.Distance(bottom, top) / height);
        }

        private bool TryInterchangeableSnap(Phase3TangramPiece dragged)
        {
            List<Vector2> current = PiecePlaneShape(dragged);
            Vector3 originalPosition = dragged.transform.position;
            Quaternion originalRotation = dragged.transform.rotation;
            Vector3 originalScale = dragged.transform.localScale;
            float radius = BoardWorldSide * 0.20f;
            float tolerance = BoardWorldSide * 0.008f;

            for (int i = 0; i < pieces.Count; i++)
            {
                Phase3TangramPiece owner = pieces[i];
                if (owner.State == TangramPieceState.Placed) continue;
                TangramTargetAssignment candidate = owner.Assignment;
                Vector3 targetCenter = LogicalToWorld(candidate.TargetPosition);
                Vector2 translation = WorldToPlane(targetCenter) - WorldToPlane(dragged.transform.position);
                if (translation.sqrMagnitude > radius * radius) continue;
                List<Vector2> targetWorld = TargetWorldPolygon(candidate);
                if (!MatchPolygonsTranslated(current, translation, targetWorld, tolerance)) continue;

                dragged.transform.position = targetCenter;
                List<Vector2> finalShape = PiecePlaneShape(dragged);
                if (!MatchPolygons(finalShape, targetWorld, tolerance) || !InsideField(finalShape) || dragged.transform.rotation != originalRotation || dragged.transform.localScale != originalScale)
                {
                    dragged.transform.position = originalPosition;
                    return false;
                }

                if (owner != dragged)
                {
                    SwapAssignments(dragged, owner);
                }

                if (!MatchPolygons(PiecePlaneShape(dragged), TargetWorldPolygon(dragged.Assignment), tolerance))
                {
                    if (owner != dragged) SwapAssignments(dragged, owner);
                    dragged.transform.position = originalPosition;
                    return false;
                }

                dragged.SetState(TangramPieceState.Placed);
                dragged.SetSortingOrder(Phase3TangramPiece.PlacedSortingOrderBase + dragged.Id);
                scoreController.AddScore(200, GamePhaseId.Phase3, ScoreReason.Other);
                CheckCompletion();
                return true;
            }
            return false;
        }

        private void CheckCompletion()
        {
            if (IsCleared) return;
            for (int i = 0; i < pieces.Count; i++) if (!pieces[i].IsPlaced) return;
            if (terminalClearCommit == null || !terminalClearCommit(GamePhaseId.Phase3)) return;
            terminalClearCommitted = true;
            IsCleared = true;
            IsRunning = false;
            ApplyInputGate();
            guide.SetVisible(false);
            for (int i = 0; i < pieces.Count; i++) pieces[i].SetVisible(false);
            completionImage.transform.parent.gameObject.SetActive(true);
            Debug.Log($"[Phase3Tangram][Completion] committed=true,pieces={pieces.Count},completionCanvasOrder={CompletionShineSortingOrder}", this);
            scoreController.AddScore(1000, GamePhaseId.Phase3, ScoreReason.Other);
            PhaseCleared?.Invoke();
            StartCompletionPresentation();
        }

        private void StartCompletionPresentation()
        {
            if (IsExitReady) return;
            StopCompletionShine();
            if (!isActiveAndEnabled || !completionShineImage || !completionSprite)
            {
                FinishCompletionPresentation();
                return;
            }
            try
            {
                bool materialReady = completionShineRuntimeMaterial && completionShineRuntimeMaterial.shader && completionShineRuntimeMaterial.shader.isSupported;
                completionShineImage.material = materialReady ? completionShineRuntimeMaterial : null;
                if (materialReady) completionShineRuntimeMaterial.SetFloat("_Progress", -CompletionShineWidth);
                else Debug.LogWarning("[Phase3Tangram] Completion shine material unavailable; using the built-in pulse fallback.", this);
                Debug.Log($"[Phase3Tangram][Completion] shineStarted=true,mode={(materialReady ? "shader" : "fallback")},duration={CompletionShineDuration:F2}", this);
                completionShineAnimation = StartCoroutine(CompletionShineRoutine());
                if (completionShineAnimation == null) FinishCompletionPresentation();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Phase3Tangram] Completion shine skipped: " + exception.Message, this);
                FinishCompletionPresentation();
            }
        }

        private IEnumerator CompletionShineRoutine()
        {
            try
            {
                completionShineImage.gameObject.SetActive(true);
                float elapsed = 0f;
                while (elapsed < CompletionShineDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float normalized = Mathf.Clamp01(elapsed / CompletionShineDuration);
                    if (completionShineImage.material == completionShineRuntimeMaterial && completionShineRuntimeMaterial)
                    {
                        completionShineImage.color = Color.white;
                        completionShineRuntimeMaterial.SetFloat("_Progress", Mathf.Lerp(-CompletionShineWidth, 2f + CompletionShineWidth, normalized));
                    }
                    else
                    {
                        float alpha = Mathf.Sin(normalized * Mathf.PI) * 0.65f;
                        completionShineImage.color = new Color(1f, 1f, 1f, alpha);
                    }
                    yield return null;
                }
            }
            finally
            {
                completionShineAnimation = null;
                if (!destroying && !completionPresentationFinishing && IsCleared && terminalClearCommitted && !IsExitReady)
                    FinishCompletionPresentation();
            }
        }

        private void FinishCompletionPresentation()
        {
            if (completionPresentationFinishing || IsExitReady) return;
            completionPresentationFinishing = true;
            try
            {
                StopCompletionShine();
                Debug.Log("[Phase3Tangram][Completion] shineFinished=true,raisingExitReady=true", this);
                RaiseExitReady();
            }
            finally
            {
                completionPresentationFinishing = false;
            }
        }

        private void StopCompletionShine()
        {
            if (completionShineAnimation != null)
            {
                StopCoroutine(completionShineAnimation);
                completionShineAnimation = null;
            }
            if (completionShineImage)
            {
                completionShineImage.color = Color.white;
                completionShineImage.gameObject.SetActive(false);
            }
        }

        private void RaiseExitReady()
        {
            if (IsExitReady) return;
            IsExitReady = true;
            PhaseExitReady?.Invoke();
        }

        private bool IsFinalUnplacedPiece(Phase3TangramPiece candidate)
        {
            if (!candidate || candidate.IsPlaced) return false;
            for (int i = 0; i < pieces.Count; i++)
            {
                Phase3TangramPiece piece = pieces[i];
                if (piece && piece != candidate && !piece.IsPlaced) return false;
            }
            return true;
        }

        private Vector3 ClampInsideField(Phase3TangramPiece piece)
        {
            List<Vector2> polygon = PiecePlaneShape(piece);
            Bounds(polygon, out Vector2 min, out Vector2 max);
            Vector3 boardMin3 = LogicalToWorld(Vector2.zero), boardMax3 = LogicalToWorld(Vector2.one * Phase3TangramGenerator.BoardSize);
            Vector2 first = WorldToPlane(boardMin3), second = WorldToPlane(boardMax3);
            Vector2 boardMin = Vector2.Min(first, second), boardMax = Vector2.Max(first, second);
            float x = min.x < boardMin.x ? boardMin.x - min.x : max.x > boardMax.x ? boardMax.x - max.x : 0f;
            float y = min.y < boardMin.y ? boardMin.y - min.y : max.y > boardMax.y ? boardMax.y - max.y : 0f;
            return piece.transform.position + PlaneVectorToWorld(new Vector2(x, y));
        }

        private void PlaceLooseInsideField(Phase3TangramPiece piece)
        {
            Vector3 corrected = ClampInsideField(piece);
            piece.transform.position = corrected;
            piece.SetState(TangramPieceState.Loose);
            piece.LastStableLoosePosition = corrected;
            piece.HasStableLoosePosition = true;
            piece.SetSortingOrder(6000 + ++looseSortingSequence);
            RefreshVisibility();
        }

        private void CancelDrop(Phase3TangramPiece piece)
        {
            if (dragOriginState == TangramPieceState.Loose && piece.HasStableLoosePosition)
            {
                piece.transform.position = piece.LastStableLoosePosition;
                piece.transform.localScale = Vector3.one * FieldWorldScale;
                piece.SetState(TangramPieceState.Loose);
                piece.SetSortingOrder(6000 + ++looseSortingSequence);
            }
            else ReturnToDeck(piece);
            RefreshVisibility();
        }

        private void ReturnToDeck(Phase3TangramPiece piece)
        {
            piece.SetState(TangramPieceState.InDeck);
            piece.HasStableLoosePosition = false;
            PlaceAtOriginalDeckSlot(piece);
            RefreshVisibility();
        }

        private void SetPage(int value)
        {
            int pageCount = (pieces.Count + 3) / 4;
            if (value < 0 || value >= pageCount) return;
            page = value;
            for (int i = 0; i < pieces.Count; i++) if (pieces[i].State == TangramPieceState.InDeck) PlaceAtOriginalDeckSlot(pieces[i]);
            RefreshVisibility();
            Debug.Log($"[Phase3Tangram][Deck] page={page + 1}/{pageCount},totalPieces={pieces.Count}", this);
        }

        private void RefreshBoardFrame()
        {
            boardFrameReady = false;
            if (!field || !worldCamera) throw new InvalidOperationException("Tangram board frame references are missing.");
            float side = Mathf.Min(field.rect.width, field.rect.height);
            Vector2 originLocal = field.rect.center - Vector2.one * side * 0.5f;
            Vector3 origin = RectLocalToWorld(field, originLocal);
            Vector3 right = RectLocalToWorld(field, originLocal + Vector2.right * side);
            Vector3 top = RectLocalToWorld(field, originLocal + Vector2.up * side);
            boardOriginWorld = origin;
            boardUnitXWorld = (right - origin) / Phase3TangramGenerator.BoardSize;
            boardUnitYWorld = (top - origin) / Phase3TangramGenerator.BoardSize;
            if (boardUnitXWorld.sqrMagnitude < 0.000001f || boardUnitYWorld.sqrMagnitude < 0.000001f)
                throw new InvalidOperationException("Tangram board frame has zero size.");
            float scaleMismatch = Mathf.Abs(boardUnitXWorld.magnitude - boardUnitYWorld.magnitude) / Mathf.Max(boardUnitXWorld.magnitude, boardUnitYWorld.magnitude);
            float orthogonality = Mathf.Abs(Vector3.Dot(boardUnitXWorld.normalized, boardUnitYWorld.normalized));
            if (scaleMismatch > 0.001f || orthogonality > 0.001f)
                throw new InvalidOperationException($"Tangram board is not square: scaleMismatch={scaleMismatch:F6}, orthogonality={orthogonality:F6}.");
            if (worldRoot)
            {
                Vector3 forward = Vector3.Cross(boardUnitXWorld, boardUnitYWorld).normalized;
                worldRoot.rotation = Quaternion.LookRotation(forward, boardUnitYWorld.normalized);
            }
            boardFrameReady = true;
        }

        private void AddInputSurface(RectTransform rect)
        {
            Image image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            rect.gameObject.AddComponent<Phase3TangramInputAdapter>().Configure(this);
        }

        private void SetRuntimeVisible(bool value)
        {
            if (phaseBackground) phaseBackground.gameObject.SetActive(value);
            if (field) field.gameObject.SetActive(value);
            if (deck) deck.gameObject.SetActive(value);
            if (worldRoot) worldRoot.gameObject.SetActive(value);
        }

        private void ClearPieces()
        {
            for (int i = 0; i < pieces.Count; i++) if (pieces[i]) Destroy(pieces[i].gameObject);
            pieces.Clear();
            selectedPiece = draggingPiece = null;
        }

        private void AdjustCanvasForWorldPieces()
        {
            if (canvasAdjusted) return;
            originalRenderMode = canvas.renderMode;
            originalCanvasCamera = canvas.worldCamera;
            originalPlaneDistance = canvas.planeDistance;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = worldCamera;
            canvas.planeDistance = 100f;
            canvasAdjusted = true;
        }

        private void RestoreCanvas()
        {
            if (!canvasAdjusted) return;
            if (canvas)
            {
                canvas.renderMode = originalRenderMode;
                canvas.worldCamera = originalCanvasCamera;
                canvas.planeDistance = originalPlaneDistance;
            }
            canvasAdjusted = false;
        }

        private void ApplyInputGate() { if (built) RefreshVisibility(); }
        private void Update() { if (InputAllowed && draggingPiece && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) RotateActive(1); }
        private void OnApplicationFocus(bool focused) { focusSuspended = !focused; ApplyInputGate(); }
        private void OnApplicationPause(bool paused) { pauseSuspended = paused; ApplyInputGate(); }

        private void OnDestroy()
        {
            destroying = true;
            StopDeckOpeningAnimation();
            StopCompletionShine();
            RestoreCanvas();
            ClearPieces();
            guide?.Dispose();
            if (completionShineRuntimeMaterial) Destroy(completionShineRuntimeMaterial);
            if (worldRoot) Destroy(worldRoot.gameObject);
        }

        private bool EnsureDeckFrames()
        {
            if (!deckFrame1) deckFrame1 = Resources.Load<Sprite>("Ingame/UI/Deck_Panel/Img_deck1");
            if (!deckFrame2) deckFrame2 = Resources.Load<Sprite>("Ingame/UI/Deck_Panel/Img_deck2");
            if (!deckFrame3) deckFrame3 = deckSprite ? deckSprite : Resources.Load<Sprite>("Ingame/UI/Deck_Panel/Img_deck3");
            return deckFrame1 && deckFrame2 && deckFrame3;
        }

        private void PlayDeckOpeningAnimation()
        {
            StopDeckOpeningAnimation();
            if (!EnsureDeckFrames()) return;
            deckAnimation = StartCoroutine(DeckOpeningRoutine());
        }

        private IEnumerator DeckOpeningRoutine()
        {
            deckPanel.preserveAspect = true;
            deckPanel.sprite = deckFrame1;
            yield return new WaitForSecondsRealtime(0.12f);
            deckPanel.sprite = deckFrame2;
            yield return new WaitForSecondsRealtime(0.12f);
            deckPanel.sprite = deckFrame3;
            deckAnimation = null;
        }

        private void StopDeckOpeningAnimation()
        {
            if (deckAnimation == null) return;
            StopCoroutine(deckAnimation);
            deckAnimation = null;
        }

        private Vector3 ProjectOntoBoardPlane(Vector3 position)
        {
            float offset = Vector3.Dot(position - BoardPlanePoint, worldCamera.transform.forward);
            return position - worldCamera.transform.forward * offset;
        }

        private Vector3 RectLocalToWorld(RectTransform rect, Vector2 local)
        {
            Vector3 world = rect.TransformPoint(local);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(EventCamera, world);
            return TryScreenToWorld(screen, out Vector3 result) ? result : Vector3.zero;
        }

        private bool TryScreenToWorld(Vector2 screen, out Vector3 world)
        {
            if (!worldCamera) { world = default; return false; }
            Ray ray = worldCamera.ScreenPointToRay(screen);
            var plane = new Plane(worldCamera.transform.forward, BoardPlanePoint);
            if (!plane.Raycast(ray, out float distance)) { world = default; return false; }
            world = ray.GetPoint(distance);
            return true;
        }

        private List<Vector2> TargetWorldPolygon(TangramTargetAssignment assignment)
        {
            var result = new List<Vector2>(assignment.AbsolutePolygon.Count);
            for (int i = 0; i < assignment.AbsolutePolygon.Count; i++) result.Add(WorldToPlane(LogicalToWorld(assignment.AbsolutePolygon[i])));
            return result;
        }

        private List<Vector2> PiecePlaneShape(Phase3TangramPiece piece)
        {
            List<Vector3> worldShape = piece.GetCurrentWorldShape();
            var result = new List<Vector2>(worldShape.Count);
            for (int i = 0; i < worldShape.Count; i++) result.Add(WorldToPlane(worldShape[i]));
            return result;
        }

        private Vector2 WorldToPlane(Vector3 world)
        {
            Vector3 offset = world - BoardPlanePoint;
            return new Vector2(Vector3.Dot(offset, worldCamera.transform.right), Vector3.Dot(offset, worldCamera.transform.up));
        }

        private Vector3 PlaneVectorToWorld(Vector2 vector)
        {
            return worldCamera.transform.right * vector.x + worldCamera.transform.up * vector.y;
        }

        private static bool PointInPolygon(Vector2 point, IReadOnlyList<Vector2> polygon)
        {
            bool inside = false;
            for (int i = 0, previous = polygon.Count - 1; i < polygon.Count; previous = i++)
            {
                Vector2 a = polygon[i], b = polygon[previous];
                bool crosses = (a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private bool InsideField(IReadOnlyList<Vector2> polygon)
        {
            Vector3 first = LogicalToWorld(Vector2.zero), second = LogicalToWorld(Vector2.one * Phase3TangramGenerator.BoardSize);
            Vector2 firstPoint = WorldToPlane(first), secondPoint = WorldToPlane(second);
            Vector2 min = Vector2.Min(firstPoint, secondPoint), max = Vector2.Max(firstPoint, secondPoint);
            for (int i = 0; i < polygon.Count; i++) if (polygon[i].x < min.x - 0.0001f || polygon[i].y < min.y - 0.0001f || polygon[i].x > max.x + 0.0001f || polygon[i].y > max.y + 0.0001f) return false;
            return true;
        }

        private static bool MatchPolygonsTranslated(IReadOnlyList<Vector2> actual, Vector2 translation, IReadOnlyList<Vector2> target, float tolerance)
        {
            var translated = new List<Vector2>(actual.Count);
            for (int i = 0; i < actual.Count; i++) translated.Add(actual[i] + translation);
            return MatchPolygons(translated, target, tolerance);
        }

        public static bool MatchPolygons(IReadOnlyList<Vector2> first, IReadOnlyList<Vector2> second, float tolerance)
        {
            if (first.Count != second.Count) return false;
            var matched = new bool[second.Count];
            float squared = tolerance * tolerance;
            for (int i = 0; i < first.Count; i++)
            {
                bool found = false;
                for (int j = 0; j < second.Count; j++) if (!matched[j] && (first[i] - second[j]).sqrMagnitude <= squared) { matched[j] = true; found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        private static void SwapAssignments(Phase3TangramPiece first, Phase3TangramPiece second)
        {
            TangramTargetAssignment assignment = first.Assignment;
            first.SetAssignment(second.Assignment);
            second.SetAssignment(assignment);
        }

        private static void Bounds(IReadOnlyList<Vector2> polygon, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue); max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < polygon.Count; i++) { min = Vector2.Min(min, polygon[i]); max = Vector2.Max(max, polygon[i]); }
        }

        private static void RotatedBounds(IReadOnlyList<Vector2> polygon, int rotationStep, out Vector2 min, out Vector2 max)
        {
            float radians = rotationStep * 45f * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians), sine = Mathf.Sin(radians);
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 point = polygon[i];
                Vector2 rotated = new Vector2(point.x * cosine - point.y * sine, point.x * sine + point.y * cosine);
                min = Vector2.Min(min, rotated);
                max = Vector2.Max(max, rotated);
            }
        }

        private static RectTransform CreateStretch(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.anchoredPosition = position; rect.sizeDelta = size; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            return rect;
        }

        private static Button CreateDeckNavigationButton(string name, Transform parent, Vector2 anchor, Vector2 position, Sprite sprite, bool mirrorHorizontally)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, position, new Vector2(97f, 148f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (mirrorHorizontally) rect.localScale = new Vector3(-1f, 1f, 1f);
            return button;
        }

        private static long CreateRandomSeed()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            long seed = BitConverter.ToInt64(bytes, 0);
            return seed != 0L ? seed : DateTime.UtcNow.Ticks;
        }
    }
}
