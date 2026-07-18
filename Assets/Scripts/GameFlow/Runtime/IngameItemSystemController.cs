using System;
using System.Collections.Generic;
using HATAGONG.Outgame;
using HATAGONG.Phase1;
using HATAGONG.Phase2;
using HATAGONG.Phase3Tangram;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.GameFlow
{
    public enum GameItemId
    {
        None = 0,
        Stopwatch = 1,
        Hammer = 2,
        TileGrinder = 3,
        TileCutter = 4,
        CementBasket = 5,
        Trowel = 6,
        Scraper = 7
    }

    [DefaultExecutionOrder(100)]
    public sealed class IngameItemSystemController : MonoBehaviour
    {
        private sealed class Slot
        {
            public Button Button;
            public Image Icon;
            public TextMeshProUGUI Quantity;
            public RectTransform QuantityPanel;
            public CanvasGroup CanvasGroup;
            public Outline ActiveOutline;
            public Image RequestBlockedOverlay;
            public ColorBlock OriginalColors;
            public bool ActiveButtonStyleApplied;
            public GameItemId ItemId;
            public UnityAction ClickHandler;
        }

        private static readonly GameItemId[][] PhaseItems =
        {
            null,
            new[] { GameItemId.Hammer, GameItemId.Scraper, GameItemId.Stopwatch },
            new[] { GameItemId.Trowel, GameItemId.CementBasket, GameItemId.Stopwatch },
            new[] { GameItemId.TileCutter, GameItemId.TileGrinder, GameItemId.Stopwatch }
        };

        private readonly Slot[] slots = new Slot[3];
        private readonly Dictionary<GameItemId, int> quantities = new Dictionary<GameItemId, int>();
        private readonly RectTransform[] overlaySegments = new RectTransform[6];
        private readonly Vector3[] worldCorners = new Vector3[4];
        private GameSessionController session;
        private GameTimerController timer;
        private Phase2PhaseAdapter phase2;
        private Phase3TangramManager phase3;
        private GamePhaseId currentPhase;
        private bool hasCurrentPhase;
        private GameItemId activeItem;
        private int activeHitCount;
        private float activeSeconds;
        private bool rewardGranted;
        private Canvas itemCanvas;
        private RectTransform activeOverlayRoot;
        private CanvasGroup activeOverlayGroup;
        private int activeOverlaySlot = -1;
        private GameItemId activeOverlayItem;
        private bool noItemDiagnosticLogged;

        private const float OverlayBorderThickness = 14f;
        private const float QuantityExclusionPadding = 10f;

        public static IngameItemSystemController Instance { get; private set; }
        public GameItemId ActiveItem => activeItem;
        public int Phase1DirectDamage => activeItem == GameItemId.Hammer ? 2 : 1;
        public bool ItemsBlockedByRequest => session && session.RunContext.HasEffect(RequestEffectRuntime.NoItem);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneBootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void BootstrapCurrentScene()
        {
            EnsureController(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureController(scene);
        }

        private static void EnsureController(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                !string.Equals(scene.name, "INGAME", StringComparison.Ordinal)) return;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameSessionController owner = roots[i].GetComponentInChildren<GameSessionController>(true);
                if (!owner) continue;
                if (!owner.GetComponent<IngameItemSystemController>())
                    owner.gameObject.AddComponent<IngameItemSystemController>();
                return;
            }
        }

        private void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
            session = GetComponent<GameSessionController>();
            timer = FindFirstObjectByType<GameTimerController>(FindObjectsInactive.Include);
            phase2 = FindFirstObjectByType<Phase2PhaseAdapter>(FindObjectsInactive.Include);
            phase3 = FindFirstObjectByType<Phase3TangramManager>(FindObjectsInactive.Include);
            LoadQuantities();
            BindExistingSlots();
            if (session)
            {
                session.SessionStateChanged += OnSessionStateChanged;
                session.GameCompleted += OnGameCompleted;
            }
            LogDiagnostic($"[ItemSystem][Initialized] instanceCount=1, owner={gameObject.name}, scene={gameObject.scene.name}, slots={slots.Length}", this);
        }

        private void OnDestroy()
        {
            if (activeItem == GameItemId.Trowel && phase2) phase2.RestoreBaseBrushRadius();
            activeItem = GameItemId.None;
            activeHitCount = 0;
            activeSeconds = 0f;
            if (session)
            {
                session.SessionStateChanged -= OnSessionStateChanged;
                session.GameCompleted -= OnGameCompleted;
            }
            for (int i = 0; i < slots.Length; i++)
                if (slots[i]?.Button && slots[i].ClickHandler != null) slots[i].Button.onClick.RemoveListener(slots[i].ClickHandler);
            if (activeOverlayRoot) Destroy(activeOverlayRoot.gameObject);
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            IGamePhase phase = session ? session.CurrentPhase : null;
            if (phase == null)
            {
                if (hasCurrentPhase) { CancelActiveItem(); hasCurrentPhase = false; RefreshSlots(); }
            }
            else if (!hasCurrentPhase || currentPhase != phase.PhaseId)
            {
                CancelActiveItem();
                currentPhase = phase.PhaseId;
                hasCurrentPhase = true;
                RefreshSlots();
            }

            if (activeItem == GameItemId.Trowel && session && session.CanAcceptGameplayInput &&
                hasCurrentPhase && currentPhase == GamePhaseId.Phase2)
            {
                activeSeconds -= Time.deltaTime;
                if (activeSeconds <= 0f) CancelActiveItem();
            }
            UpdateVisualState();
        }

        private void LateUpdate()
        {
            if (activeOverlayRoot && activeOverlayRoot.gameObject.activeSelf && activeOverlaySlot >= 0)
                UpdateOverlayGeometry(activeOverlaySlot, out _, out _);
        }

        public void NotifyPhase1DirectHit(Phase1TileView directTile, IReadOnlyList<Phase1TileView> allTiles, Func<Phase1TileView, bool> applyItemDamage)
        {
            if (activeItem == GameItemId.Hammer)
            {
                if (++activeHitCount >= 8) CancelActiveItem();
                return;
            }
            if (activeItem != GameItemId.Scraper || allTiles == null || applyItemDamage == null) return;
            var candidates = new List<Phase1TileView>();
            for (int i = 0; i < allTiles.Count; i++)
            {
                Phase1TileView tile = allTiles[i];
                if (tile && tile != directTile && !tile.IsDestroyed && tile.CanReceiveDamage) candidates.Add(tile);
            }
            int count = Mathf.Min(2, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int index = UnityEngine.Random.Range(0, candidates.Count);
                Phase1TileView target = candidates[index];
                candidates.RemoveAt(index);
                applyItemDamage(target);
            }
            if (++activeHitCount >= 8) CancelActiveItem();
        }

        private void BindExistingSlots()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                int slotNumber = i + 1;
                GameObject buttonObject = FindNamedObjectInScene(gameObject.scene, "Item_Button0" + slotNumber);
                if (!buttonObject) throw new InvalidOperationException("Required INGAME item button is missing: Item_Button0" + slotNumber);
                var slot = new Slot
                {
                    Button = buttonObject.GetComponent<Button>(),
                    Icon = FindNamedChild<Image>(buttonObject.transform, "Item_Icon0" + slotNumber),
                    Quantity = FindQuantityText(buttonObject.transform, slotNumber),
                    QuantityPanel = FindNamedChild<RectTransform>(buttonObject.transform, "Item_ValuePanel0" + slotNumber),
                    CanvasGroup = buttonObject.GetComponent<CanvasGroup>() ?? buttonObject.AddComponent<CanvasGroup>(),
                    OriginalColors = buttonObject.GetComponent<Button>() ? buttonObject.GetComponent<Button>().colors : ColorBlock.defaultColorBlock
                };
                if (!slot.Button || !slot.Icon || !slot.Quantity)
                    throw new InvalidOperationException($"INGAME item slot references are incomplete: {slotNumber}, button={!!slot.Button}, icon={!!slot.Icon}, quantity={!!slot.Quantity}");
                slot.Icon.preserveAspect = true;
                slot.Icon.raycastTarget = false;
                slot.Quantity.raycastTarget = false;
                Transform legacyOutline = buttonObject.transform.Find("ActiveOutline");
                if (legacyOutline) legacyOutline.gameObject.SetActive(false);
                Graphic buttonGraphic = slot.Button.targetGraphic;
                if (!buttonGraphic)
                    throw new InvalidOperationException($"INGAME item slot button Graphic is missing: {slotNumber}");
                slot.ActiveOutline = buttonGraphic.gameObject.AddComponent<Outline>();
                slot.ActiveOutline.effectColor = new Color(1f, 0.72f, 0.08f, 1f);
                slot.ActiveOutline.effectDistance = new Vector2(7f, 7f);
                slot.ActiveOutline.useGraphicAlpha = false;
                slot.ActiveOutline.enabled = false;
                slot.RequestBlockedOverlay = CreateRequestBlockedOverlay(buttonObject.transform, slotNumber);
                slots[i] = slot;
            }

            EnsureActiveOverlay();

            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                int captured = i;
                slot.ClickHandler = () => TryUseSlot(captured);
                slot.Button.onClick.AddListener(slot.ClickHandler);
            }
            RefreshSlots();
        }

        private void TryUseSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                LogRejected("InvalidSlot", slotIndex, null);
                return;
            }
            Slot slot = slots[slotIndex];
            LogClick(slotIndex, slot);
            string gateReason = GetUseGateRejectionReason();
            if (!string.IsNullOrEmpty(gateReason))
            {
                LogRejected(gateReason, slotIndex, slot);
                return;
            }
            GameItemId itemId = slot.ItemId;
            if (itemId == GameItemId.None) { LogRejected("ItemNotMapped", slotIndex, slot); return; }
            if (activeItem != GameItemId.None) { LogRejected("ActiveItemLocked", slotIndex, slot); return; }
            if (GetQuantity(itemId) <= 0) { LogRejected("NoInventory", slotIndex, slot); return; }
            bool applied = false;
            switch (itemId)
            {
                case GameItemId.Stopwatch:
                    applied = timer && timer.TryAddSeconds(5d, 99d);
                    break;
                case GameItemId.Hammer:
                case GameItemId.Scraper:
                    applied = HasPhase1Target();
                    if (applied) { activeItem = itemId; activeHitCount = 0; }
                    break;
                case GameItemId.Trowel:
                    applied = phase2 && phase2.TrySetBrushRadiusMultiplier(1.4f);
                    if (applied) { activeItem = itemId; activeSeconds = 6f; }
                    break;
                case GameItemId.CementBasket:
                    applied = phase2 && phase2.TryApplyRandomUnpaintedFraction(UnityEngine.Random.Range(0.08f, 0.1200001f), out _);
                    break;
                case GameItemId.TileCutter:
                    applied = phase3 && phase3.TryAutoPlaceEligiblePiece();
                    break;
                case GameItemId.TileGrinder:
                    applied = phase3 && phase3.TryCorrectEligibleRotations(2, out _);
                    break;
            }
            if (!applied)
            {
                LogRejected(GetEffectRejectionReason(itemId), slotIndex, slot);
                return;
            }
            if (!PlayerProgressRepository.TryConsumeItem(itemId))
            {
                Debug.LogError("[Items] Effect applied but inventory persistence failed for " + itemId, this);
                if (IsPersistent(itemId)) CancelActiveItem();
                return;
            }
            quantities[itemId] = Mathf.Max(0, GetQuantity(itemId) - 1);
            RefreshSlots();
            PlaySuccessfulItemSound(itemId);
            LogActiveVisualState(slotIndex);
            LogDiagnostic($"[ItemSystem][Applied] slot={slotIndex + 1}, itemId={itemId}, inventoryCount={GetQuantity(itemId)}, activeItem={activeItem}", this);
        }

        private bool CanUseItems()
        {
            return string.IsNullOrEmpty(GetUseGateRejectionReason());
        }

        private static void PlaySuccessfulItemSound(GameItemId itemId)
        {
            switch (itemId)
            {
                case GameItemId.Stopwatch: GameSfxPlayer.Play(GameSfxId.Time); break;
                case GameItemId.CementBasket: GameSfxPlayer.Play(GameSfxId.Basket); break;
                case GameItemId.TileCutter: GameSfxPlayer.Play(GameSfxId.Cutter); break;
                case GameItemId.TileGrinder: GameSfxPlayer.Play(GameSfxId.Grinder); break;
                case GameItemId.Hammer:
                case GameItemId.Scraper:
                case GameItemId.Trowel:
                    GameSfxPlayer.Play(GameSfxId.Use);
                    break;
            }
        }

        private string GetUseGateRejectionReason()
        {
            if (!session) return "SessionMissing";
            if (ItemsBlockedByRequest) return "RequestEffectNoItem";
            if (session.CurrentState != GameSessionState.Playing) return "SessionNotPlaying";
            if (!session.CanAcceptGameplayInput) return "GameplayInputBlocked";
            if (session.IsExpired) return "SessionExpired";
            if (session.IsCompleted) return "SessionCompleted";
            if (session.IsTransitioning) return "SessionTransitioning";
            if (session.IsSceneLoadRequested) return "SceneLoadRequested";
            if (session.CurrentPhase == null) return "PhaseMissing";
            if (!session.CurrentPhase.IsPrepared) return "PhaseNotPrepared";
            if (!session.CurrentPhase.IsRunning) return "PhaseNotRunning";
            if (session.CurrentPhase.IsCleared) return "PhaseCleared";
            if (session.CurrentPhase.IsExitReady) return "PhaseExitReady";
            if (!hasCurrentPhase || currentPhase != session.CurrentPhase.PhaseId) return "PhaseNotSynchronized";
            return string.Empty;
        }

        private string GetEffectRejectionReason(GameItemId itemId)
        {
            if (itemId == GameItemId.Stopwatch)
            {
                if (!timer) return "TimerMissing";
                if (!timer.IsRunning) return "TimerNotRunning";
                if (timer.RemainingSeconds >= 99d) return "TimerAtMaximum";
            }
            if (itemId == GameItemId.CementBasket) return "NoEligibleUnpaintedCells";
            return "NoEligibleTarget";
        }

        private bool HasPhase1Target()
        {
            if (!hasCurrentPhase || currentPhase != GamePhaseId.Phase1) return false;
            Phase1TileView[] tiles = FindObjectsByType<Phase1TileView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < tiles.Length; i++) if (tiles[i] && !tiles[i].IsDestroyed && tiles[i].CanReceiveDamage) return true;
            return false;
        }

        private void OnSessionStateChanged(GameSessionState state)
        {
            if (state != GameSessionState.Playing) CancelActiveItem();
            RefreshSlots();
        }

        private void OnGameCompleted()
        {
            CancelActiveItem();
            if (rewardGranted) return;
            rewardGranted = true;
            if (!PlayerProgressRepository.GrantFullClearItemReward())
                Debug.LogError("[Items] Full-clear item reward could not be saved.", this);
            LoadQuantities();
            RefreshSlots();
        }

        private void CancelActiveItem()
        {
            if (activeItem == GameItemId.Trowel && phase2) phase2.RestoreBaseBrushRadius();
            activeItem = GameItemId.None;
            activeHitCount = 0;
            activeSeconds = 0f;
            RefreshSlots();
        }

        private void RefreshSlots()
        {
            GameItemId[] mapping = hasCurrentPhase && (int)currentPhase > 0 && (int)currentPhase < PhaseItems.Length
                ? PhaseItems[(int)currentPhase]
                : null;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot == null) continue;
                slot.ItemId = mapping == null ? GameItemId.None : mapping[i];
                slot.Icon.sprite = ResolveIcon(slot.ItemId);
                slot.Icon.enabled = slot.Icon.sprite;
                slot.Quantity.text = slot.ItemId == GameItemId.None ? string.Empty : GetQuantity(slot.ItemId).ToString();
            }
            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            bool usableState = CanUseItems();
            bool blockedByRequestForAll = ItemsBlockedByRequest;
            float pulse = 0.825f + Mathf.Sin(Time.unscaledTime * 2.4f) * 0.175f;
            int selectedActiveSlot = -1;
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot == null) continue;
                bool isActive = activeItem != GameItemId.None && slot.ItemId == activeItem;
                bool blockedByActive = activeItem != GameItemId.None && !isActive;
                bool blockedByRequest = blockedByRequestForAll;
                bool interactable = usableState && activeItem == GameItemId.None && slot.ItemId != GameItemId.None && GetQuantity(slot.ItemId) > 0;
                slot.Button.interactable = interactable;
                if (slot.ActiveOutline) slot.ActiveOutline.enabled = isActive && !blockedByRequest;
                if (slot.RequestBlockedOverlay) slot.RequestBlockedOverlay.gameObject.SetActive(blockedByRequest);
                if (!slot.CanvasGroup) continue;
                slot.CanvasGroup.alpha = 1f;
                slot.CanvasGroup.blocksRaycasts = interactable;
                if (slot.ActiveButtonStyleApplied != isActive)
                {
                    ColorBlock colors = slot.OriginalColors;
                    if (activeItem != GameItemId.None) colors.disabledColor = colors.normalColor;
                    slot.Button.colors = colors;
                    slot.ActiveButtonStyleApplied = isActive;
                }
                float visualAlpha = blockedByActive || blockedByRequest ? 0.42f : 1f;
                if (slot.Button.targetGraphic) slot.Button.targetGraphic.canvasRenderer.SetAlpha(visualAlpha);
                slot.Icon.canvasRenderer.SetAlpha(visualAlpha);
                slot.Quantity.canvasRenderer.SetAlpha(blockedByRequest ? 0.42f : 1f);
                if (slot.QuantityPanel)
                {
                    Graphic panelGraphic = slot.QuantityPanel.GetComponent<Graphic>();
                    if (panelGraphic) panelGraphic.canvasRenderer.SetAlpha(1f);
                }
                if (isActive && !blockedByRequest) selectedActiveSlot = i;
            }

            if (selectedActiveSlot >= 0)
            {
                ShowActiveOverlay(selectedActiveSlot, pulse);
            }
            else HideActiveOverlay("ActiveItemCleared");

            if (!noItemDiagnosticLogged && session && session.RunContext.IsValid)
            {
                int visibleXCount = 0;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i]?.RequestBlockedOverlay && slots[i].RequestBlockedOverlay.gameObject.activeSelf) visibleXCount++;
                Debug.Log($"[RequestEffect][NoItem] active={blockedByRequestForAll}, blockedButtons={(blockedByRequestForAll ? slots.Length : 0)}, visibleX={visibleXCount}, useBlocked={blockedByRequestForAll}, consumptionBlocked={blockedByRequestForAll}", this);
                noItemDiagnosticLogged = true;
            }
        }

        private static Image CreateRequestBlockedOverlay(Transform button, int slotNumber)
        {
            Sprite offSprite = Resources.Load<Sprite>("Ingame/ICON/Img_icon_opuix");
            if (!offSprite) throw new InvalidOperationException("Settings UI X Sprite is missing: Ingame/ICON/Img_icon_opuix");
            Transform existing = button.Find("RequestNoItemOverlay");
            GameObject overlayObject = existing
                ? existing.gameObject
                : new GameObject("RequestNoItemOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayObject.layer = button.gameObject.layer;
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            if (!existing) rect.SetParent(button, false);
            rect.anchorMin = new Vector2(0.14f, 0.14f);
            rect.anchorMax = new Vector2(0.86f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.SetAsLastSibling();
            Image image = overlayObject.GetComponent<Image>();
            image.sprite = offSprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            image.gameObject.SetActive(false);
            return image;
        }

        private void LoadQuantities()
        {
            foreach (GameItemId itemId in Enum.GetValues(typeof(GameItemId)))
                if (itemId != GameItemId.None) quantities[itemId] = PlayerProgressRepository.GetItemQuantity(itemId);
        }

        private int GetQuantity(GameItemId itemId) => quantities.TryGetValue(itemId, out int value) ? Mathf.Clamp(value, 0, 99) : 0;
        private static bool IsPersistent(GameItemId itemId) => itemId == GameItemId.Hammer || itemId == GameItemId.Scraper || itemId == GameItemId.Trowel;

        private static Sprite ResolveIcon(GameItemId itemId)
        {
            string key;
            switch (itemId)
            {
                case GameItemId.Stopwatch: key = "Img_icon_item1"; break;
                case GameItemId.Hammer: key = "Img_icon_item2"; break;
                case GameItemId.TileGrinder: key = "Img_icon_item3"; break;
                case GameItemId.TileCutter: key = "Img_icon_item4"; break;
                case GameItemId.CementBasket: key = "Img_icon_item5"; break;
                case GameItemId.Trowel: key = "Img_icon_item6"; break;
                case GameItemId.Scraper: key = "Img_icon_item7"; break;
                default: return null;
            }
            Sprite sprite = Resources.Load<Sprite>("Ingame/Item/" + key);
            if (!sprite) Debug.LogError("[Items] Missing Sprite: " + key);
            return sprite;
        }

        private static T FindNamedChild<T>(Transform parent, string name) where T : Component
        {
            T[] values = parent.GetComponentsInChildren<T>(true);
            for (int i = 0; i < values.Length; i++) if (values[i].name == name) return values[i];
            return null;
        }

        private static TextMeshProUGUI FindQuantityText(Transform button, int slotNumber)
        {
            string expectedName = "Item_Value0" + slotNumber;
            TextMeshProUGUI exact = FindNamedChild<TextMeshProUGUI>(button, expectedName);
            if (exact) return exact;

            Transform panel = FindNamedChild<Transform>(button, "Item_ValuePanel0" + slotNumber);
            TextMeshProUGUI[] candidates = panel
                ? panel.GetComponentsInChildren<TextMeshProUGUI>(true)
                : button.GetComponentsInChildren<TextMeshProUGUI>(true);
            if (candidates.Length != 1) return null;

            Debug.LogWarning($"[ItemSystem] Quantity TMP name mismatch in slot {slotNumber}. expected={expectedName}, actual={candidates[0].name}; existing object is reused without hierarchy changes.", button);
            return candidates[0];
        }

        private static GameObject FindNamedObjectInScene(Scene scene, string name)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform found = FindNamedChild<Transform>(roots[i].transform, name);
                if (found) return found.gameObject;
            }
            return null;
        }

        private void LogClick(int slotIndex, Slot slot)
        {
            IGamePhase phase = session ? session.CurrentPhase : null;
            LogDiagnostic($"[ItemSystem][Click] slot={slotIndex + 1}, itemId={slot?.ItemId}, controller={!!this}, " +
                $"sessionState={(session ? session.CurrentState.ToString() : "Missing")}, currentPhase={(phase != null ? phase.PhaseId.ToString() : "Missing")}, " +
                $"phaseRunning={phase?.IsRunning == true}, inputAllowed={session?.CanAcceptGameplayInput == true}, " +
                $"inventoryCount={(slot != null ? GetQuantity(slot.ItemId) : 0)}, activeItem={activeItem}, buttonInteractable={slot?.Button && slot.Button.interactable}", this);
        }

        private void LogRejected(string reason, int slotIndex, Slot slot)
        {
            LogDiagnostic($"[ItemSystem][Rejected] reason={reason}, slot={slotIndex + 1}, itemId={slot?.ItemId}", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogActiveVisualState(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null) return;
            Slot slot = slots[slotIndex];
            Debug.Log($"[ItemActiveVisual][RenderedState] item={slot.ItemId}, slot={slotIndex + 1}, mode=ButtonGraphicOutline, outlineEnabled={slot.ActiveOutline && slot.ActiveOutline.enabled}, graphic={slot.Button.targetGraphic?.name}, quantityVisible={slot.Quantity && slot.Quantity.gameObject.activeInHierarchy}, activeItem={activeItem}", slot.Button);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDiagnostic(string message, UnityEngine.Object context)
        {
            Debug.Log(message, context);
        }

        private void EnsureActiveOverlay()
        {
            Canvas nearestCanvas = slots[0]?.Button ? slots[0].Button.GetComponentInParent<Canvas>() : null;
            itemCanvas = nearestCanvas ? nearestCanvas.rootCanvas : null;
            if (!itemCanvas) throw new InvalidOperationException("INGAME item Canvas is missing.");

            Transform existing = itemCanvas.transform.Find("ItemActiveOverlayRoot");
            GameObject root = existing ? existing.gameObject : new GameObject("ItemActiveOverlayRoot", typeof(RectTransform), typeof(CanvasGroup));
            activeOverlayRoot = root.GetComponent<RectTransform>() ?? root.AddComponent<RectTransform>();
            if (!existing) activeOverlayRoot.SetParent(itemCanvas.transform, false);
            root.layer = slots[0].Button.gameObject.layer;
            activeOverlayRoot.anchorMin = Vector2.zero;
            activeOverlayRoot.anchorMax = Vector2.one;
            activeOverlayRoot.pivot = new Vector2(0.5f, 0.5f);
            activeOverlayRoot.offsetMin = Vector2.zero;
            activeOverlayRoot.offsetMax = Vector2.zero;
            activeOverlayRoot.localScale = Vector3.one;
            activeOverlayRoot.localRotation = Quaternion.identity;
            activeOverlayRoot.SetAsLastSibling();
            activeOverlayGroup = root.GetComponent<CanvasGroup>() ?? root.AddComponent<CanvasGroup>();
            activeOverlayGroup.alpha = 0f;
            activeOverlayGroup.interactable = false;
            activeOverlayGroup.blocksRaycasts = false;

            string[] segmentNames = { "BorderTop", "BorderLeft", "BorderBottomLeft", "BorderBottomRight", "BorderRightTop", "BorderRightBottom" };
            for (int i = 0; i < segmentNames.Length; i++)
            {
                Transform child = activeOverlayRoot.Find(segmentNames[i]);
                GameObject segment = child ? child.gameObject : new GameObject(segmentNames[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                segment.layer = root.layer;
                RectTransform rect = segment.GetComponent<RectTransform>();
                if (!child) rect.SetParent(activeOverlayRoot, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;
                rect.localRotation = Quaternion.identity;
                Image image = segment.GetComponent<Image>() ?? segment.AddComponent<Image>();
                image.sprite = null;
                image.material = null;
                image.type = Image.Type.Simple;
                image.color = new Color(1f, 0.72f, 0.08f, 1f);
                image.raycastTarget = false;
                image.enabled = true;
                overlaySegments[i] = rect;
            }
            root.SetActive(false);
        }

        private void ShowActiveOverlay(int slotIndex, float pulse)
        {
            if (!activeOverlayRoot || slotIndex < 0 || slotIndex >= slots.Length) return;
            Slot slot = slots[slotIndex];
            bool firstFrame = !activeOverlayRoot.gameObject.activeSelf || activeOverlaySlot != slotIndex || activeOverlayItem != slot.ItemId;
            activeOverlaySlot = slotIndex;
            activeOverlayItem = slot.ItemId;
            activeOverlayRoot.SetAsLastSibling();
            activeOverlayRoot.gameObject.SetActive(true);
            activeOverlayGroup.alpha = firstFrame ? 1f : pulse;
            bool quantityFound = UpdateOverlayGeometry(slotIndex, out Rect buttonBounds, out Rect quantityBounds);
            if (firstFrame) LogActiveOverlayShow(slotIndex, slot, buttonBounds, quantityFound, quantityBounds);
        }

        private void HideActiveOverlay(string reason)
        {
            if (!activeOverlayRoot || !activeOverlayRoot.gameObject.activeSelf)
            {
                activeOverlaySlot = -1;
                activeOverlayItem = GameItemId.None;
                return;
            }
            GameItemId hiddenItem = activeOverlayItem;
            activeOverlayGroup.alpha = 0f;
            activeOverlayRoot.gameObject.SetActive(false);
            activeOverlaySlot = -1;
            activeOverlayItem = GameItemId.None;
            LogDiagnostic($"[ItemActiveOverlay][Hide] item={hiddenItem}, reason={reason}, activeItemAfter={activeItem}", this);
        }

        private bool UpdateOverlayGeometry(int slotIndex, out Rect buttonBounds, out Rect quantityBounds)
        {
            buttonBounds = default;
            quantityBounds = default;
            if (!activeOverlayRoot || slotIndex < 0 || slotIndex >= slots.Length || slots[slotIndex] == null) return false;
            Slot slot = slots[slotIndex];
            if (!TryGetOverlayLocalRect(slot.Button.transform as RectTransform, out buttonBounds)) return false;

            bool quantityFound = false;
            if (slot.QuantityPanel) quantityFound = TryGetOverlayLocalRect(slot.QuantityPanel, out quantityBounds);
            if (TryGetOverlayLocalRect(slot.Quantity.rectTransform, out Rect textBounds))
            {
                quantityBounds = quantityFound ? Union(quantityBounds, textBounds) : textBounds;
                quantityFound = true;
            }

            float thickness = Mathf.Min(OverlayBorderThickness, Mathf.Min(buttonBounds.width, buttonBounds.height) * 0.15f);
            SetOverlaySegment(0, buttonBounds.xMin, buttonBounds.yMax - thickness, buttonBounds.xMax, buttonBounds.yMax);
            SetOverlaySegment(1, buttonBounds.xMin, buttonBounds.yMin, buttonBounds.xMin + thickness, buttonBounds.yMax);

            if (quantityFound)
            {
                Rect excluded = Rect.MinMaxRect(
                    quantityBounds.xMin - QuantityExclusionPadding,
                    quantityBounds.yMin - QuantityExclusionPadding,
                    quantityBounds.xMax + QuantityExclusionPadding,
                    quantityBounds.yMax + QuantityExclusionPadding);
                float leftEnd = Mathf.Clamp(excluded.xMin, buttonBounds.xMin, buttonBounds.xMax);
                float rightStart = Mathf.Clamp(excluded.xMax, buttonBounds.xMin, buttonBounds.xMax);
                float lowerEnd = Mathf.Clamp(excluded.yMin, buttonBounds.yMin, buttonBounds.yMax);
                float upperStart = Mathf.Clamp(excluded.yMax, buttonBounds.yMin, buttonBounds.yMax);
                SetOverlaySegment(2, buttonBounds.xMin, buttonBounds.yMin, leftEnd, buttonBounds.yMin + thickness);
                SetOverlaySegment(3, rightStart, buttonBounds.yMin, buttonBounds.xMax, buttonBounds.yMin + thickness);
                SetOverlaySegment(4, buttonBounds.xMax - thickness, upperStart, buttonBounds.xMax, buttonBounds.yMax);
                SetOverlaySegment(5, buttonBounds.xMax - thickness, buttonBounds.yMin, buttonBounds.xMax, lowerEnd);
            }
            else
            {
                SetOverlaySegment(2, buttonBounds.xMin, buttonBounds.yMin, buttonBounds.xMax, buttonBounds.yMin + thickness);
                SetOverlaySegment(3, 0f, 0f, 0f, 0f);
                SetOverlaySegment(4, buttonBounds.xMax - thickness, buttonBounds.yMin, buttonBounds.xMax, buttonBounds.yMax);
                SetOverlaySegment(5, 0f, 0f, 0f, 0f);
            }
            return quantityFound;
        }

        private bool TryGetOverlayLocalRect(RectTransform source, out Rect result)
        {
            result = default;
            if (!source || !source.gameObject.activeInHierarchy || !activeOverlayRoot) return false;
            source.GetWorldCorners(worldCorners);
            Vector3 first = activeOverlayRoot.InverseTransformPoint(worldCorners[0]);
            float minX = first.x, maxX = first.x, minY = first.y, maxY = first.y;
            for (int i = 1; i < worldCorners.Length; i++)
            {
                Vector3 point = activeOverlayRoot.InverseTransformPoint(worldCorners[i]);
                minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
            }
            result = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return result.width > 0f && result.height > 0f;
        }

        private void SetOverlaySegment(int index, float minX, float minY, float maxX, float maxY)
        {
            RectTransform segment = overlaySegments[index];
            bool visible = maxX - minX > 0.5f && maxY - minY > 0.5f;
            segment.gameObject.SetActive(visible);
            if (!visible) return;
            segment.anchoredPosition = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
            segment.sizeDelta = new Vector2(maxX - minX, maxY - minY);
        }

        private static Rect Union(Rect left, Rect right)
        {
            return Rect.MinMaxRect(
                Mathf.Min(left.xMin, right.xMin),
                Mathf.Min(left.yMin, right.yMin),
                Mathf.Max(left.xMax, right.xMax),
                Mathf.Max(left.yMax, right.yMax));
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogActiveOverlayShow(int slotIndex, Slot slot, Rect buttonBounds, bool quantityFound, Rect quantityBounds)
        {
            int activeSegments = 0;
            for (int i = 0; i < overlaySegments.Length; i++) if (overlaySegments[i] && overlaySegments[i].gameObject.activeSelf) activeSegments++;
            Debug.Log($"[ItemActiveOverlay][Show] item={slot.ItemId}, slot={slotIndex + 1}, activeItem={activeItem}, buttonActiveInHierarchy={slot.Button.gameObject.activeInHierarchy}, canvasName={itemCanvas.name}, canvasRenderMode={itemCanvas.renderMode}, buttonWorldBounds={buttonBounds}, overlayLocalBounds={buttonBounds}, quantityRectFound={quantityFound}, quantityBounds={quantityBounds}, borderSegmentCount={activeSegments}, borderActive={activeOverlayRoot.gameObject.activeSelf}, borderAlpha={activeOverlayGroup.alpha:F2}, overlaySiblingIndex={activeOverlayRoot.GetSiblingIndex()}", activeOverlayRoot);
        }
    }
}
