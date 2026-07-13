using System;
using HATAGONG.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HATAGONG.Phase3
{
    public interface IPhase3SessionSource
    {
        bool TryCreateSession(GameRunContext context, out Phase3PuzzleSessionModel session, out string failureReason);
    }

    /// <summary>
    /// Production bridge between the shared game-flow lifecycle and the Phase 3
    /// puzzle/runtime presentation. The scene owns this component and its root;
    /// the generated children are the real INGAME presentation hierarchy.
    /// </summary>
    public sealed class Phase3PhaseAdapter : MonoBehaviour, IGamePhase
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private GraphicRaycaster graphicRaycaster;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private GameScoreController scoreController;
        [SerializeField] private RectTransform deckHost;
        [SerializeField] private Image deckPanel;
        [SerializeField] private Sprite deckSprite;
        [SerializeField] private MonoBehaviour sessionSource;

        private Phase3RuntimeBinding binding;
        private Phase3RuntimeOrchestrator orchestrator;
        private Phase3DesktopInputController desktopInput;
        private Phase3PuzzleSessionModel session;
        private RectTransform deckPresentationRoot;
        private RectTransform dragPresentationRoot;
        private GameObject fallbackBadge;
        private bool runtimeBuilt;
        private bool subscribed;
        private bool phaseClearedRaised;
        private bool exitReadyRaised;
        private bool inputRequested;
        private bool focusSuspended;
        private bool pauseSuspended;

        public GamePhaseId PhaseId => GamePhaseId.Phase3;
        public bool IsPrepared { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsExitReady { get; private set; }
        public bool InputEnabled => orchestrator && orchestrator.InputEnabled;
        public string LastFailureReason { get; private set; } = string.Empty;
        public int RuntimeGenerationCount { get; private set; }
        public Phase3PuzzleSessionModel Session => session;
        public bool UsingFallbackTemplate { get; private set; }
        public string ActiveTemplateId { get; private set; } = string.Empty;

        public event Action PhaseCleared;
        public event Action PhaseExitReady;

        public bool Prepare(GameRunContext context)
        {
            SetInputEnabled(false);
            LastFailureReason = string.Empty;
            IsPrepared = false;
            IsRunning = false;
            IsCleared = false;
            IsExitReady = false;
            phaseClearedRaised = false;
            exitReadyRaised = false;

            if (!ApplyDeckSprite() || !context.IsValid || !canvas || !graphicRaycaster || !eventSystem || !scoreController || !deckHost)
            {
                return FailPrepare("Invalid context or required Phase 3 Production scene reference.");
            }

            try
            {
                EnsureRuntimeBuilt();
                EnsureSubscribed();
                orchestrator.UnbindSession();

                if (!TryCreateSession(context, out session, out string sessionError))
                {
                    return FailPrepare(sessionError);
                }
                orchestrator.BindSession(session);
                desktopInput.Bind(orchestrator);
                fallbackBadge.SetActive(UsingFallbackTemplate);

                IsPrepared = true;
                RuntimeGenerationCount++;
                deckPresentationRoot.gameObject.SetActive(false);
                dragPresentationRoot.gameObject.SetActive(false);
                gameObject.SetActive(false);
                return true;
            }
            catch (Exception exception)
            {
                return FailPrepare(exception.Message);
            }
        }

        public bool Activate()
        {
            SetInputEnabled(false);
            if (!ApplyDeckSprite() || !IsPrepared || session == null || !orchestrator || session.IsCleared)
            {
                LastFailureReason = "Phase 3 is not prepared for activation.";
                return false;
            }

            gameObject.SetActive(true);
            deckPresentationRoot.gameObject.SetActive(true);
            dragPresentationRoot.gameObject.SetActive(true);
            IsRunning = true;
            orchestrator.RefreshAllViews();
            return true;
        }

        public void Deactivate()
        {
            SetInputEnabled(false);
            IsRunning = false;
            if (deckPresentationRoot) deckPresentationRoot.gameObject.SetActive(false);
            if (dragPresentationRoot) dragPresentationRoot.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        public void SetInputEnabled(bool enabled)
        {
            inputRequested = enabled;
            ApplyInputGate();
        }

        private void ApplyInputGate()
        {
            bool allowed = inputRequested && !focusSuspended && !pauseSuspended && IsPrepared && IsRunning && !IsCleared && !IsExitReady;
            if (orchestrator) orchestrator.SetInputEnabled(allowed);
        }

        private void EnsureRuntimeBuilt()
        {
            if (runtimeBuilt)
            {
                binding.ValidateOrThrow();
                return;
            }

            if (transform.childCount != 0)
            {
                throw new InvalidOperationException("Phase3Root must not contain unmanaged Production children.");
            }

            binding = gameObject.GetComponent<Phase3RuntimeBinding>() ?? gameObject.AddComponent<Phase3RuntimeBinding>();
            orchestrator = gameObject.GetComponent<Phase3RuntimeOrchestrator>() ?? gameObject.AddComponent<Phase3RuntimeOrchestrator>();
            desktopInput = gameObject.GetComponent<Phase3DesktopInputController>() ?? gameObject.AddComponent<Phase3DesktopInputController>();
            Phase3MobileInputController mobileInput = gameObject.GetComponent<Phase3MobileInputController>() ?? gameObject.AddComponent<Phase3MobileInputController>();

            RectTransform field = CreateRect("Field", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1250f, 1250f));
            Phase3EmptyFieldSurface emptySurface = CreateEmptyFieldSurface(field);
            RectTransform targets = CreateStretchRect("TargetLayer", field);
            RectTransform placed = CreateStretchRect("PlacedLayer", field);
            RectTransform loose = CreateStretchRect("LooseLayer", field);
            RectTransform drag = CreateDragOverlay(deckHost);
            dragPresentationRoot = drag;

            RectTransform deck = CreateDeck(deckHost, out RectTransform[] deckSlots, out Button previous, out Button next);
            deckPresentationRoot = deck;
            Button rotate = CreateButton("Rotate", transform, new Vector2(1f, 1f), new Vector2(-76f, -76f), new Vector2(128f, 128f), new Color(0.14f, 0.45f, 0.86f, 0.96f));
            CreateRotateIcon(rotate.transform as RectTransform);
            fallbackBadge = CreateFallbackBadge(transform);

            binding.Configure(
                canvas,
                graphicRaycaster,
                eventSystem,
                field,
                emptySurface,
                targets,
                placed,
                loose,
                drag,
                deck,
                deckSlots,
                previous,
                next,
                rotate,
                mobileInput);
            binding.ValidateOrThrow();
            orchestrator.Configure(binding);
            emptySurface.Bind(mobileInput);
            desktopInput.Bind(orchestrator);
            runtimeBuilt = true;
        }

        private bool TryCreateSession(GameRunContext context, out Phase3PuzzleSessionModel created, out string failureReason)
        {
            created = null;
            failureReason = string.Empty;
            UsingFallbackTemplate = false;
            ActiveTemplateId = string.Empty;

            if (sessionSource)
            {
                if (!(sessionSource is IPhase3SessionSource source))
                {
                    failureReason = $"Configured Phase 3 session source does not implement {nameof(IPhase3SessionSource)}.";
                    return false;
                }
                if (!source.TryCreateSession(context, out created, out failureReason) || created == null)
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason)
                        ? "Configured Phase 3 session source did not provide a session."
                        : failureReason;
                    return false;
                }
                if (created.Difficulty != context.Difficulty || created.IsCleared || created.HasActiveDrag)
                {
                    failureReason = "Configured Phase 3 session source returned an invalid initial session state.";
                    created = null;
                    return false;
                }
                return true;
            }

            Phase3SafeTemplate template = Phase3SafeTemplateCatalog.GetDefault(context.Difficulty);
            created = new Phase3PuzzleSessionModel(template.Puzzle, context.Difficulty, template.InitialRotations);
            UsingFallbackTemplate = true;
            ActiveTemplateId = template.TemplateId;
            Debug.LogWarning($"[Phase3][Production] No real session source is configured. Development Safe Template fallback is active: {ActiveTemplateId}.", this);
            return true;
        }

        private void EnsureSubscribed()
        {
            if (subscribed) return;
            orchestrator.OperationResolved += OnOperationResolved;
            orchestrator.PhaseCleared += OnRuntimeCleared;
            subscribed = true;
        }

        private void OnOperationResolved(Phase3PlayResult result)
        {
            if (!result.IsSuccess || result.TotalScoreDelta <= 0) return;
            if (scoreController.AddScore(result.TotalScoreDelta, GamePhaseId.Phase3, ScoreReason.Other)) return;

            LastFailureReason = "GameScoreController rejected a Phase 3 score delta.";
            SetInputEnabled(false);
        }

        private void OnRuntimeCleared()
        {
            if (IsCleared || !string.IsNullOrEmpty(LastFailureReason)) return;
            IsCleared = true;
            IsRunning = false;
            SetInputEnabled(false);

            if (!phaseClearedRaised)
            {
                phaseClearedRaised = true;
                PhaseCleared?.Invoke();
            }

            // Phase 3 is the final playable phase. Until the dedicated result-flow
            // stage is implemented, GameSessionController intentionally enters its
            // existing safe locked transition after this exit-ready signal.
            if (!exitReadyRaised)
            {
                exitReadyRaised = true;
                IsExitReady = true;
                PhaseExitReady?.Invoke();
            }
        }

        private bool FailPrepare(string reason)
        {
            LastFailureReason = reason ?? "Unknown Phase 3 preparation failure.";
            if (orchestrator) orchestrator.UnbindSession();
            session = null;
            UsingFallbackTemplate = false;
            ActiveTemplateId = string.Empty;
            IsPrepared = false;
            IsRunning = false;
            IsCleared = false;
            IsExitReady = false;
            if (deckPresentationRoot) deckPresentationRoot.gameObject.SetActive(false);
            if (dragPresentationRoot) dragPresentationRoot.gameObject.SetActive(false);
            gameObject.SetActive(false);
            Debug.LogError($"[Phase3][Production] Prepare failed: {LastFailureReason}", this);
            return false;
        }

        private void OnDisable()
        {
            if (orchestrator) orchestrator.SetInputEnabled(false);
            IsRunning = false;
            if (deckPresentationRoot) deckPresentationRoot.gameObject.SetActive(false);
            if (dragPresentationRoot) dragPresentationRoot.gameObject.SetActive(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            focusSuspended = !focused;
            ApplyInputGate();
        }

        private void OnApplicationPause(bool paused)
        {
            pauseSuspended = paused;
            ApplyInputGate();
        }

        private void OnDestroy()
        {
            if (subscribed && orchestrator)
            {
                orchestrator.OperationResolved -= OnOperationResolved;
                orchestrator.PhaseCleared -= OnRuntimeCleared;
            }
            subscribed = false;
            if (orchestrator) orchestrator.UnbindSession();
            session = null;
            if (deckPresentationRoot)
            {
                if (Application.isPlaying) Destroy(deckPresentationRoot.gameObject);
                else DestroyImmediate(deckPresentationRoot.gameObject);
            }
            if (dragPresentationRoot)
            {
                if (Application.isPlaying) Destroy(dragPresentationRoot.gameObject);
                else DestroyImmediate(dragPresentationRoot.gameObject);
            }
        }

        private static Phase3EmptyFieldSurface CreateEmptyFieldSurface(RectTransform parent)
        {
            RectTransform rect = CreateStretchRect("EmptyFieldSurface", parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            image.raycastTarget = true;
            return rect.gameObject.AddComponent<Phase3EmptyFieldSurface>();
        }

        private bool ApplyDeckSprite()
        {
            if (!deckPanel || !deckSprite) return false;
            deckPanel.sprite = deckSprite;
            deckPanel.preserveAspect = true;
            return true;
        }

        private static RectTransform CreateDragOverlay(RectTransform deck)
        {
            RectTransform deckContainer = deck.parent as RectTransform;
            RectTransform commonRoot = deckContainer ? deckContainer.parent as RectTransform : null;
            if (!commonRoot) throw new InvalidOperationException("Phase 3 Deck must belong to the shared INGAME UI hierarchy.");
            RectTransform overlay = CreateStretchRect("Phase3DragOverlay", commonRoot);
            overlay.SetSiblingIndex(deckContainer.GetSiblingIndex() + 1);
            return overlay;
        }

        private static GameObject CreateFallbackBadge(Transform parent)
        {
            RectTransform badge = CreateRect("DevelopmentSafeTemplateBadge", parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(230f, -36f), new Vector2(440f, 56f));
            Image background = badge.gameObject.AddComponent<Image>();
            background.color = new Color(0.86f, 0.52f, 0.08f, 0.94f);
            background.raycastTarget = false;

            RectTransform labelRect = CreateStretchRect("Label", badge);
            TextMeshProUGUI label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = "DEVELOPMENT SAFE TEMPLATE";
            label.fontSize = 25f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            return badge.gameObject;
        }

        private static RectTransform CreateDeck(Transform parent, out RectTransform[] slots, out Button previous, out Button next)
        {
            RectTransform deck = CreateStretchRect("Phase3DeckContent", parent);
            Image background = deck.gameObject.AddComponent<Image>();
            background.color = Color.clear;
            background.raycastTarget = true;

            previous = CreateButton("PreviousPage", deck, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(72f, 170f), new Color(0.18f, 0.42f, 0.78f, 0.9f));
            next = CreateButton("NextPage", deck, new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(72f, 170f), new Color(0.18f, 0.42f, 0.78f, 0.9f));
            CreateArrowIcon(previous.transform as RectTransform, false);
            CreateArrowIcon(next.transform as RectTransform, true);

            slots = new RectTransform[Phase3RuntimeInputPolicy.DeckPageSize];
            float[] x = { -480f, -160f, 160f, 480f };
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i] = CreateRect($"DeckSlot{i + 1}", deck, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x[i], -2f), new Vector2(250f, 190f));
                Image slotBackground = slots[i].gameObject.AddComponent<Image>();
                slotBackground.color = Color.clear;
                slotBackground.raycastTarget = true;
            }
            return deck;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, anchor, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.16f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.2f);
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
            button.colors = colors;
            return button;
        }

        private static void CreateArrowIcon(RectTransform parent, bool right)
        {
            RectTransform icon = CreateRect("Icon", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42f, 72f));
            Phase3PolygonGraphic graphic = icon.gameObject.AddComponent<Phase3PolygonGraphic>();
            float sign = right ? 1f : -1f;
            graphic.SetVertices(new[] { new Vector2(-18f * sign, -32f), new Vector2(20f * sign, 0f), new Vector2(-18f * sign, 32f) });
            graphic.color = Color.white;
            graphic.raycastTarget = false;
        }

        private static void CreateRotateIcon(RectTransform parent)
        {
            RectTransform icon = CreateRect("Icon", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(70f, 70f));
            Phase3PolygonGraphic graphic = icon.gameObject.AddComponent<Phase3PolygonGraphic>();
            graphic.SetVertices(new[] { new Vector2(0f, 32f), new Vector2(32f, 0f), new Vector2(0f, -32f), new Vector2(-32f, 0f) });
            graphic.color = Color.white;
            graphic.raycastTarget = false;
        }

        private static RectTransform CreateStretchRect(string name, Transform parent)
        {
            return CreateRect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return rect;
        }
    }
}
