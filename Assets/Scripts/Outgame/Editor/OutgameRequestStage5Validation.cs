using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HATAGONG.GameFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.Outgame.Editor
{
    public static class OutgameRequestStage5Validation
    {
        public const int ExpectedEditModeAssertions = 36;
        public const int ExpectedPlayModeAssertions = 64;

        private const string ScenePath = "Assets/Scenes/OUTGAME_LOBBY.unity";
        private const string GeneralPath = "LobbyCanvas/Outgame_UI_General";
        private const string QuestPath = GeneralPath + "/ContentLayer/LobbyNPC/QuestIndicator";
        private const string PopupLayerPath = GeneralPath + "/RequestPopupLayer";
        private const string ViewportPath = PopupLayerPath + "/RequestPopupRoot/RequestViewport";
        private const string ContentPath = ViewportPath + "/RequestContent";
        private const string PageIndicatorPath = PopupLayerPath + "/RequestPopupRoot/PageIndicator";

        private const string PlayRequestedKey = "HATAGONG.Outgame.Stage5.PlayRequested";
        private const string PlayRunningKey = "HATAGONG.Outgame.Stage5.PlayRunning";
        private const string PlayAwaitingExitKey = "HATAGONG.Outgame.Stage5.PlayAwaitingExit";
        private const string PlayPassedKey = "HATAGONG.Outgame.Stage5.PlayPassed";
        private const string PlayTotalKey = "HATAGONG.Outgame.Stage5.PlayTotal";
        private const string PlayFailuresKey = "HATAGONG.Outgame.Stage5.PlayFailures";
        private const string SceneDirtyKey = "HATAGONG.Outgame.Stage5.SceneDirty";
        private const string SceneRootCountKey = "HATAGONG.Outgame.Stage5.SceneRootCount";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/HATAGONG/Outgame/Setup Stage 5 Assets")]
        public static void SetupStage5Assets()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("OUTGAME_LOBBY must be active.");

            GameObject general = GameObject.Find(GeneralPath);
            GameObject quest = GameObject.Find(QuestPath);
            Transform popupLayerTransform = general == null ? null : general.transform.Find("RequestPopupLayer");
            GameObject popupLayer = popupLayerTransform == null ? null : popupLayerTransform.gameObject;
            if (general == null || quest == null || popupLayer == null)
                throw new InvalidOperationException("Stage 4 Scene hierarchy is incomplete.");

            Transform popupRoot = popupLayer.transform.Find("RequestPopupRoot");
            RectTransform viewport = popupRoot.Find("RequestViewport") as RectTransform;
            RectTransform content = viewport.Find("RequestContent") as RectTransform;
            Image dimImage = popupLayer.transform.Find("Dim").GetComponent<Image>();
            OutgameRequestPopupView popupView = popupLayer.GetComponent<OutgameRequestPopupView>();
            Image viewportInput = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
            viewportInput.sprite = null;
            viewportInput.color = new Color(1f, 1f, 1f, 0.001f);
            viewportInput.raycastTarget = true;
            viewportInput.maskable = true;

            DestroyImmediateIfPresent<OutgameRequestSelectionController>(general);
            DestroyImmediateIfPresent<OutgameRequestCarouselController>(viewport.gameObject);
            DestroyImmediateIfPresent<ScrollRect>(viewport.gameObject);
            DestroyImmediateIfPresent<Button>(dimImage.gameObject);
            Transform oldIndicator = popupRoot.Find("PageIndicator");
            if (oldIndicator != null) UnityEngine.Object.DestroyImmediate(oldIndicator.gameObject);

            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 0f;

            Button dimButton = dimImage.gameObject.AddComponent<Button>();
            dimButton.targetGraphic = dimImage;
            dimButton.transition = Selectable.Transition.None;

            RectTransform indicatorRoot = CreateRect("PageIndicator", popupRoot as RectTransform, new Vector2(0f, -1060f), new Vector2(120f, 28f));
            var pageImages = new Image[3];
            for (int i = 0; i < pageImages.Length; i++)
            {
                RectTransform page = CreateRect("Page" + (i + 1), indicatorRoot, new Vector2(-40f + (40f * i), 0f), new Vector2(22f, 22f));
                pageImages[i] = page.gameObject.AddComponent<Image>();
                pageImages[i].color = i == 0 ? new Color32(255, 200, 48, 255) : new Color32(145, 145, 145, 180);
                pageImages[i].raycastTarget = false;
            }

            OutgameRequestCarouselController carousel = viewport.gameObject.AddComponent<OutgameRequestCarouselController>();
            var carouselSerialized = new SerializedObject(carousel);
            SetReference(carouselSerialized, "scrollRect", scroll);
            SetReference(carouselSerialized, "viewport", viewport);
            SetReference(carouselSerialized, "content", content);
            SetReferenceArray(carouselSerialized, "pageIndicators", pageImages.Cast<UnityEngine.Object>().ToArray());
            carouselSerialized.ApplyModifiedPropertiesWithoutUndo();

            OutgameRequestSelectionController selection = general.AddComponent<OutgameRequestSelectionController>();
            var selectionSerialized = new SerializedObject(selection);
            SetReference(selectionSerialized, "questIndicatorButton", quest.GetComponent<Button>());
            SetReference(selectionSerialized, "requestPopupLayer", popupLayer);
            SetReference(selectionSerialized, "dimButton", dimButton);
            SetReference(selectionSerialized, "popupView", popupView);
            SetReference(selectionSerialized, "carouselController", carousel);
            selectionSerialized.ApplyModifiedPropertiesWithoutUndo();

            popupLayer.transform.SetAsLastSibling();
            popupLayer.SetActive(false);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Outgame][Stage5][Setup] Selection, ScrollRect, Carousel and PageIndicator configured.");
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 5 EditMode")]
        public static void ValidateEditMode()
        {
            var validation = new ValidationState();
            try { RunEditModeValidation(validation); }
            catch (Exception exception) { validation.Fail("Unexpected EditMode exception: " + exception); }
            LogResult("EditMode", validation, ExpectedEditModeAssertions);
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 5 PlayMode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Outgame][Stage5][PlayMode] A Play Mode transition is already active.");
                return;
            }
            Scene scene = EditorSceneManager.GetActiveScene();
            SessionState.SetBool(PlayRequestedKey, true);
            SessionState.SetBool(PlayRunningKey, false);
            SessionState.SetBool(PlayAwaitingExitKey, false);
            SessionState.SetBool(SceneDirtyKey, scene.isDirty);
            SessionState.SetInt(SceneRootCountKey, scene.rootCount);
            FocusGameView();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(PlayRequestedKey, false) &&
                !SessionState.GetBool(PlayRunningKey, false))
            {
                SessionState.SetBool(PlayRunningKey, true);
                RunPlayModeValidationAsync();
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode ||
                !SessionState.GetBool(PlayAwaitingExitKey, false)) return;

            var validation = new ValidationState(
                SessionState.GetInt(PlayPassedKey, 0),
                SessionState.GetInt(PlayTotalKey, 0),
                DeserializeFailures(SessionState.GetString(PlayFailuresKey, string.Empty)));
            Scene scene = EditorSceneManager.GetActiveScene();
            validation.Check(string.Equals(scene.path, ScenePath, StringComparison.Ordinal), "OUTGAME_LOBBY remains active");
            validation.Check(scene.isDirty == SessionState.GetBool(SceneDirtyKey, false), "Scene dirty state remains unchanged");
            validation.Check(scene.rootCount == SessionState.GetInt(SceneRootCountKey, -1), "Scene root count remains unchanged");
            ClearPlaySession();
            LogResult("PlayMode", validation, ExpectedPlayModeAssertions);
        }

        private static void RunEditModeValidation(ValidationState validation)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            validation.Check(string.Equals(scene.path, ScenePath, StringComparison.Ordinal), "OUTGAME_LOBBY is active");
            GameObject general = GameObject.Find(GeneralPath);
            validation.Check(general != null, "Outgame_UI_General exists");
            OutgameRequestSelectionController selection = general == null ? null : general.GetComponent<OutgameRequestSelectionController>();
            validation.Check(selection != null, "Selection Controller exists");
            validation.Check(selection != null && selection.gameObject.activeInHierarchy, "Selection Controller is on active General root");

            var selectionSerialized = selection == null ? null : new SerializedObject(selection);
            validation.Check(HasReference(selectionSerialized, "questIndicatorButton"), "QuestIndicator Button reference");
            validation.Check(HasReference(selectionSerialized, "requestPopupLayer"), "RequestPopupLayer reference");
            validation.Check(HasReference(selectionSerialized, "dimButton"), "Dim Button reference");
            validation.Check(HasReference(selectionSerialized, "popupView"), "Popup View reference");
            validation.Check(HasReference(selectionSerialized, "carouselController"), "Carousel reference");

            GameObject quest = GameObject.Find(QuestPath);
            validation.Check(quest != null && quest.GetComponent<Button>() != null, "QuestIndicator path and Button");
            Transform popupLayerTransform = general == null ? null : general.transform.Find("RequestPopupLayer");
            GameObject popupLayer = popupLayerTransform == null ? null : popupLayerTransform.gameObject;
            validation.Check(popupLayer != null && !popupLayer.activeSelf, "Popup Layer is inactive by default");
            Transform pageRoot = popupLayer == null ? null : popupLayer.transform.Find("RequestPopupRoot/PageIndicator");
            validation.Check(pageRoot != null, "PageIndicator exists");

            GameObject viewportObject = popupLayer == null
                ? null
                : popupLayer.transform.Find("RequestPopupRoot/RequestViewport")?.gameObject;
            ScrollRect scroll = viewportObject == null ? null : viewportObject.GetComponent<ScrollRect>();
            validation.Check(scroll != null, "ScrollRect exists");
            validation.Check(scroll != null && scroll.horizontal, "ScrollRect horizontal is on");
            validation.Check(scroll != null && !scroll.vertical, "ScrollRect vertical is off");
            validation.Check(scroll != null && scroll.movementType == ScrollRect.MovementType.Clamped, "ScrollRect movement is Clamped");
            validation.Check(scroll != null && scroll.content != null && scroll.content.name == "RequestContent", "ScrollRect Content reference");
            validation.Check(scroll != null && scroll.viewport != null && scroll.viewport.name == "RequestViewport", "ScrollRect Viewport reference");
            validation.Check(scroll != null && Mathf.Approximately(scroll.scrollSensitivity, 0f), "Mouse wheel sensitivity is zero");
            validation.Check(viewportObject != null && viewportObject.GetComponent<OutgameRequestCarouselController>() != null, "Carousel is on Viewport");
            Image viewportInput = viewportObject == null ? null : viewportObject.GetComponent<Image>();
            validation.Check(viewportInput != null, "Viewport input Image exists");
            validation.Check(viewportInput != null && viewportInput.raycastTarget, "Viewport input Image raycast is on");
            validation.Check(viewportInput != null && viewportInput.color.a > 0f && viewportInput.color.a <= 0.0011f, "Viewport input Image is visually transparent but nonzero");

            Image[] pages = pageRoot == null ? Array.Empty<Image>() : pageRoot.GetComponentsInChildren<Image>(true);
            validation.Check(pages.Length == 3, "Exactly three Page Images");
            validation.Check(pageRoot != null && Enumerable.Range(1, 3).All(index => pageRoot.Find("Page" + index) != null), "Page1, Page2 and Page3 exist");
            validation.Check(pages.All(value => !value.raycastTarget), "Page Images raycast is off");
            validation.Check(popupLayer != null && popupLayer.GetComponentsInChildren<Canvas>(true).Length == 0, "Additional Canvas count is zero");
            validation.Check(popupLayer != null && popupLayer.GetComponentsInChildren<EventSystem>(true).Length == 0, "Additional EventSystem count is zero");

            Button perform = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Outgame/Requests/OutgameRequestCard.prefab")
                .transform.Find("PerformButton").GetComponent<Button>();
            validation.Check(!perform.interactable, "PerformButton remains disabled");
            validation.Check(perform.onClick.GetPersistentEventCount() == 0, "PerformButton OnClick remains zero");
            validation.Check(quest != null && quest.GetComponent<Button>().onClick.GetPersistentEventCount() == 0, "Quest persistent OnClick is zero");
            Button dim = popupLayer == null ? null : popupLayer.transform.Find("Dim").GetComponent<Button>();
            validation.Check(dim != null && dim.onClick.GetPersistentEventCount() == 0, "Dim persistent OnClick is zero");
            validation.Check(popupLayer != null && !HasMissingComponent(popupLayer), "Popup has no Missing Script");
            validation.Check(popupLayer != null && popupLayer.transform.GetSiblingIndex() == popupLayer.transform.parent.childCount - 1, "Popup remains last child");
            validation.Check(pageRoot != null && ((RectTransform)pageRoot).anchoredPosition.y < ((RectTransform)viewportObject.transform).rect.yMin, "PageIndicator is below Viewport");
            validation.Check(general != null && general.GetComponents<OutgameRequestSelectionController>().Length == 1, "Exactly one Selection Controller");
        }

        private static async void RunPlayModeValidationAsync()
        {
            var validation = new ValidationState();
            try
            {
                OutgameRequestSelectionController selection = Resources.FindObjectsOfTypeAll<OutgameRequestSelectionController>()
                    .FirstOrDefault(value => value.gameObject.scene.IsValid());
                validation.Check(selection != null, "Selection Controller exists in Play Mode");
                if (selection == null) throw new InvalidOperationException("Selection Controller is missing.");

                GameObject popupLayer = GameObject.Find(PopupLayerPath);
                validation.Check(popupLayer == null || !popupLayer.activeSelf, "Popup starts inactive");
                Button quest = GameObject.Find(QuestPath).GetComponent<Button>();
                validation.Check(quest != null, "QuestIndicator Button exists");
                validation.Check(!quest.interactable, "QuestIndicator is disabled before load completes");

                float deadline = Time.realtimeSinceStartup + 8f;
                while (!selection.IsReady && Time.realtimeSinceStartup < deadline) await Task.Yield();
                validation.Check(selection.IsReady, "Selection becomes ready");
                validation.Check(selection.LoadAttemptCount == 1, "CSV loads once");
                validation.Check(selection.BatchSeedGenerationCount == 1, "BatchSeed is generated once");
                validation.Check(selection.Batch != null, "Prepared Batch exists");
                validation.Check(selection.Batch != null && selection.Batch.Offers.Count == 3, "Prepared Batch has three offers");
                validation.Check(quest.interactable, "QuestIndicator is enabled after load");

                popupLayer = Resources.FindObjectsOfTypeAll<OutgameRequestPopupView>()
                    .First(value => value.gameObject.scene.IsValid()).gameObject;
                Click(quest);
                await AwaitEditorUpdate();
                await AwaitEditorUpdate();
                Canvas.ForceUpdateCanvases();
                OutgameRequestPopupView popup = popupLayer.GetComponent<OutgameRequestPopupView>();
                OutgameRequestCarouselController carousel = popupLayer.GetComponentInChildren<OutgameRequestCarouselController>(true);
                validation.Check(popupLayer.activeSelf, "Quest click opens Popup");
                validation.Check(popup.Cards.Count == 3, "Popup displays three cards");
                validation.Check(carousel.PageCount == 3, "Carousel has three pages");
                validation.Check(carousel.CurrentPage == 0, "Popup opens on first page");
                validation.Check(IndicatorsMatch(carousel), "First PageIndicator is selected");
                Image viewportInput = carousel.Viewport.GetComponent<Image>();
                validation.Check(viewportInput != null, "Runtime Viewport input Image exists");
                validation.Check(viewportInput != null && viewportInput.raycastTarget, "Runtime Viewport input Image receives raycasts");

                RaycastDragResult firstDrag = await DragToAdjacentPage(carousel, -1f);
                validation.Check(firstDrag.TopTarget != null, "Card blank area has a GraphicRaycaster result");
                validation.Check(firstDrag.Handler != null &&
                    (firstDrag.Handler.GetComponent<ScrollRect>() != null || firstDrag.Handler.GetComponent<OutgameRequestCarouselController>() != null),
                    "Blank-area Raycast resolves to the ScrollRect or Carousel hierarchy");
                validation.Check(firstDrag.TopTarget != null && firstDrag.TopTarget.name != "Dim", "Dim is not the top Raycast target over cards");
                validation.Check(carousel.CurrentPage == 1, "Pointer drag moves 0 to 1");
                validation.Check(carousel.IsPageAligned(), "Page 1 is aligned");
                validation.Check(IndicatorsMatch(carousel), "PageIndicator matches page 1");
                await DragToAdjacentPage(carousel, -1f);
                validation.Check(carousel.CurrentPage == 2, "Pointer drag moves 1 to 2");
                validation.Check(carousel.IsPageAligned(), "Page 2 is aligned");
                validation.Check(IndicatorsMatch(carousel), "PageIndicator matches page 2");
                await DragToAdjacentPage(carousel, -1f);
                validation.Check(carousel.CurrentPage == 2, "Dragging beyond page 2 is clamped");
                validation.Check(carousel.IsPageAligned(), "Last page remains aligned");
                await DragToAdjacentPage(carousel, 1f);
                validation.Check(carousel.CurrentPage == 1, "Pointer drag moves 2 to 1");
                validation.Check(carousel.IsPageAligned(), "Return page 1 is aligned");
                validation.Check(IndicatorsMatch(carousel), "PageIndicator matches return page 1");
                await DragToAdjacentPage(carousel, 1f);
                validation.Check(carousel.CurrentPage == 0, "Pointer drag moves 1 to 0");
                validation.Check(carousel.IsPageAligned(), "Return page 0 is aligned");
                validation.Check(IndicatorsMatch(carousel), "PageIndicator matches return page 0");
                await DragToAdjacentPage(carousel, -1f, 0.05f);
                validation.Check(carousel.CurrentPage == 0 && carousel.IsPageAligned(), "Short drag below threshold keeps page 0");

                int preparedSeed = selection.BatchSeed;
                string preparedSignature = BatchSignature(selection.Batch);
                Button dim = popupLayer.transform.Find("Dim").GetComponent<Button>();
                Click(dim);
                validation.Check(!popupLayer.activeSelf, "Dim click closes Popup");
                Click(quest);
                await Task.Yield();
                validation.Check(popupLayer.activeSelf, "Quest click reopens Popup");
                validation.Check(BatchSignature(selection.Batch) == preparedSignature, "Reopen keeps RequestIds and order");
                validation.Check(selection.BatchSeed == preparedSeed, "Reopen keeps BatchSeed");
                validation.Check(selection.BatchSeedGenerationCount == 1, "Reopen generates no new BatchSeed");
                validation.Check(carousel.CurrentPage == 0, "Reopen resets to first page");
                validation.Check(IndicatorsMatch(carousel), "Reopen PageIndicator selects first page");

                OutgameRequestOfferBatch twoBatch = OutgameRequestOfferGenerator.Generate(CreateCatalog(2), 7).Batch;
                validation.Check(twoBatch != null && twoBatch.Offers.Count == 3, "Two-request Fixture creates three offers");
                validation.Check(twoBatch != null && twoBatch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == 2, "Two-request Fixture keeps both IDs");
                validation.Check(twoBatch != null &&
                    twoBatch.Offers.Any(value => value.Definition.RequestId == "STAGE5_0") &&
                    twoBatch.Offers.Any(value => value.Definition.RequestId == "STAGE5_1"),
                    "Both two-request definitions appear");
                validation.Check(twoBatch != null && twoBatch.Offers.GroupBy(value => value.Definition.RequestId).Count(group => group.Count() == 2) == 1, "Exactly one two-request definition is duplicated");
                validation.Check(twoBatch != null && DuplicateContractsMatch(twoBatch), "Two-request duplicate Difficulty and Seeds match");
                popup.Bind(twoBatch);
                carousel.Initialize(3);
                validation.Check(popup.Cards.Count == 3, "Two-request Fixture displays three cards");

                OutgameRequestOfferBatch oneBatch = OutgameRequestOfferGenerator.Generate(CreateCatalog(1), 7).Batch;
                validation.Check(oneBatch != null && oneBatch.Offers.Count == 3, "One-request Fixture creates three offers");
                validation.Check(oneBatch != null && oneBatch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == 1, "One-request Fixture repeats one ID");
                validation.Check(oneBatch != null && DuplicateContractsMatch(oneBatch), "One-request duplicate Difficulty and Seeds match");
                popup.Bind(oneBatch);
                carousel.Initialize(3);
                validation.Check(popup.Cards.Count == 3, "One-request Fixture displays three cards");

                Canvas.ForceUpdateCanvases();
                validation.Check(FirstCardIsCentered(carousel, popup), "First card is centered in Viewport");
                validation.Check(carousel.Viewport.GetComponent<RectMask2D>() != null, "Viewport mask remains active");
                Transform pageRoot = popupLayer.transform.Find("RequestPopupRoot/PageIndicator");
                validation.Check(((RectTransform)pageRoot).anchoredPosition.y < carousel.Viewport.rect.yMin, "PageIndicator is below cards");
                validation.Check(pageRoot.GetComponentsInChildren<Image>(true).Length == 3, "Exactly three PageIndicators remain");
                TextMeshProUGUI[] texts = popup.GetComponentsInChildren<TextMeshProUGUI>(true);
                validation.Check(texts.All(value => value.text.IndexOf('\uFFFD') < 0 && value.text.IndexOf('\u25A1') < 0), "Missing glyph and replacement squares are zero");
                validation.Check(popup.Cards.All(value => !value.IsPerformButtonInteractable), "PerformButtons remain disabled");
                validation.Check(popupLayer.GetComponentsInChildren<Canvas>(true).Length == 0 && popupLayer.GetComponentsInChildren<EventSystem>(true).Length == 0, "No additional Canvas or EventSystem");
                validation.Check(carousel.IsPageAligned(), "Final first page has no blank offset");
                validation.Check(pageRoot.GetComponentsInChildren<Image>(true).All(value => value.gameObject.activeInHierarchy), "All PageIndicators are visible");

                selection.ClosePopup();
            }
            catch (Exception exception)
            {
                validation.Fail("Unexpected PlayMode exception: " + exception);
            }
            finally
            {
                SessionState.SetInt(PlayPassedKey, validation.Passed);
                SessionState.SetInt(PlayTotalKey, validation.Total);
                SessionState.SetString(PlayFailuresKey, SerializeFailures(validation.Failures));
                SessionState.SetBool(PlayAwaitingExitKey, true);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        private static async Task<RaycastDragResult> DragToAdjacentPage(OutgameRequestCarouselController carousel, float direction, float distanceRatio = 0.16f)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) throw new InvalidOperationException("EventSystem is missing.");
            RectTransform viewport = carousel.Viewport;
            Canvas canvas = viewport.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3 blankWorld = viewport.TransformPoint(new Vector2(viewport.rect.xMin + 24f, viewport.rect.center.y));
            Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, blankWorld);
            float screenWidth = viewport.rect.width * canvas.scaleFactor;
            var data = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = center,
                pressPosition = center
            };
            var raycasts = new List<RaycastResult>();
            float raycastDeadline = Time.realtimeSinceStartup + 2f;
            do
            {
                raycasts.Clear();
                eventSystem.RaycastAll(data, raycasts);
                if (raycasts.Count > 0) break;
                await AwaitEditorUpdate();
                Canvas.ForceUpdateCanvases();
            }
            while (Time.realtimeSinceStartup < raycastDeadline);
            GameObject topTarget = raycasts.Count == 0 ? null : raycasts[0].gameObject;
            if (topTarget == null) return new RaycastDragResult(null, null);
            data.pointerCurrentRaycast = raycasts[0];
            data.pointerPressRaycast = raycasts[0];
            ExecuteEvents.ExecuteHierarchy(topTarget, data, ExecuteEvents.pointerDownHandler);
            GameObject handler = ExecuteEvents.GetEventHandler<IBeginDragHandler>(topTarget);
            if (handler == null) return new RaycastDragResult(topTarget, null);
            data.pointerDrag = handler;
            ExecuteEvents.Execute(handler, data, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(handler, data, ExecuteEvents.beginDragHandler);
            data.dragging = true;
            float dragDistance = screenWidth * distanceRatio;
            if (distanceRatio >= 0.12f)
                dragDistance = Mathf.Max(dragDistance, Mathf.Max(60f, screenWidth * 0.12f) + 5f);
            data.position = center + new Vector2(direction * dragDistance, 0f);
            ExecuteEvents.Execute(handler, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(handler, data, ExecuteEvents.endDragHandler);
            await Task.Delay(350);
            return new RaycastDragResult(topTarget, handler);
        }

        private static Task AwaitEditorUpdate()
        {
            var completion = new TaskCompletionSource<bool>();
            EditorApplication.CallbackFunction callback = null;
            callback = () =>
            {
                EditorApplication.update -= callback;
                completion.TrySetResult(true);
            };
            EditorApplication.update += callback;
            return completion.Task;
        }

        private static void FocusGameView()
        {
            Type gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return;
            EditorWindow gameView = EditorWindow.GetWindow(gameViewType);
            gameView.Focus();
            gameView.Repaint();
        }

        private sealed class RaycastDragResult
        {
            public RaycastDragResult(GameObject topTarget, GameObject handler) { TopTarget = topTarget; Handler = handler; }
            public GameObject TopTarget { get; }
            public GameObject Handler { get; }
        }

        private static void Click(Button button)
        {
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        private static bool IndicatorsMatch(OutgameRequestCarouselController carousel)
        {
            Transform root = carousel.transform.parent.Find("PageIndicator");
            Image[] indicators = root.GetComponentsInChildren<Image>(true);
            if (indicators.Length != 3) return false;
            Color selected = indicators[carousel.CurrentPage].color;
            for (int i = 0; i < indicators.Length; i++)
            {
                if (i == carousel.CurrentPage) continue;
                if (indicators[i].color == selected || indicators[i].color != indicators[(carousel.CurrentPage + 1) % 3].color && i != (carousel.CurrentPage + 1) % 3)
                    return false;
            }
            return true;
        }

        private static bool FirstCardIsCentered(OutgameRequestCarouselController carousel, OutgameRequestPopupView popup)
        {
            if (popup.Cards.Count == 0) return false;
            Vector3 cardCenter = ((RectTransform)popup.Cards[0].transform).TransformPoint(((RectTransform)popup.Cards[0].transform).rect.center);
            Vector3 viewportCenter = carousel.Viewport.TransformPoint(carousel.Viewport.rect.center);
            return Vector2.Distance(cardCenter, viewportCenter) <= 1f;
        }

        private static bool DuplicateContractsMatch(OutgameRequestOfferBatch batch)
        {
            foreach (IGrouping<string, OutgameRequestOffer> group in batch.Offers.GroupBy(value => value.Definition.RequestId))
            {
                OutgameRequestOffer[] offers = group.ToArray();
                OutgameRequestOffer first = offers[0];
                if (offers.Any(value =>
                    value.Definition.Difficulty != first.Definition.Difficulty ||
                    value.Definition.RequestType != first.Definition.RequestType ||
                    value.Definition.PermanentSeed != first.Definition.PermanentSeed ||
                    value.Phase1Seed != first.Phase1Seed ||
                    value.Phase2Seed != first.Phase2Seed ||
                    value.Phase3Seed != first.Phase3Seed)) return false;
            }
            return true;
        }

        private static OutgameRequestCatalog CreateCatalog(int enabledCount)
        {
            var requests = new List<OutgameRequestDefinition>();
            int count = Math.Max(1, enabledCount);
            for (int i = 0; i < count; i++)
            {
                requests.Add(new OutgameRequestDefinition(
                    "STAGE5_" + i,
                    i < enabledCount,
                    i % 2 == 0 ? RequestType.Normal : RequestType.Sudden,
                    i % 2 == 0 ? GameDifficulty.Easy : GameDifficulty.Hard,
                    850001 + i,
                    "Fixture",
                    "fixture",
                    "Fixture",
                    "Fixture",
                    Array.Empty<OutgameRequestEffectDefinition>()));
            }
            ConstructorInfo constructor = typeof(OutgameRequestCatalog).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(IEnumerable<OutgameRequestDefinition>), typeof(IEnumerable<OutgameRequestEffectDefinition>) },
                null);
            return (OutgameRequestCatalog)constructor.Invoke(new object[] { requests, Array.Empty<OutgameRequestEffectDefinition>() });
        }

        private static string BatchSignature(OutgameRequestOfferBatch batch)
        {
            return string.Join("|", batch.Offers.Select(value => value.Definition.RequestId + ":" + value.Phase1Seed + ":" + value.Phase2Seed + ":" + value.Phase3Seed));
        }

        private static bool HasReference(SerializedObject serialized, string name)
        {
            return serialized != null && serialized.FindProperty(name).objectReferenceValue != null;
        }

        private static bool HasMissingComponent(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.GetComponents<Component>().Any(component => component == null)) return true;
            return false;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void DestroyImmediateIfPresent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        private static void SetReference(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }

        private static void SetReferenceArray(SerializedObject serialized, string name, UnityEngine.Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static string SerializeFailures(IReadOnlyList<string> failures)
        {
            return string.Join("\u001E", failures.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))));
        }

        private static IReadOnlyList<string> DeserializeFailures(string value)
        {
            if (string.IsNullOrEmpty(value)) return Array.Empty<string>();
            var failures = new List<string>();
            foreach (string encoded in value.Split(new[] { '\u001E' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try { failures.Add(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))); }
                catch (FormatException) { failures.Add(encoded); }
            }
            return failures;
        }

        private static void ClearPlaySession()
        {
            SessionState.EraseBool(PlayRequestedKey);
            SessionState.EraseBool(PlayRunningKey);
            SessionState.EraseBool(PlayAwaitingExitKey);
            SessionState.EraseInt(PlayPassedKey);
            SessionState.EraseInt(PlayTotalKey);
            SessionState.EraseString(PlayFailuresKey);
            SessionState.EraseBool(SceneDirtyKey);
            SessionState.EraseInt(SceneRootCountKey);
        }

        private static void LogResult(string mode, ValidationState validation, int expected)
        {
            if (validation.Total != expected) validation.Fail($"Assertion total mismatch: expected={expected}, actual={validation.Total}");
            string message = $"[Outgame][Stage5][{mode}] result={validation.Passed}/{validation.Total}, expected={expected}, failures={validation.Failures.Count}";
            if (validation.Failures.Count == 0) Debug.Log(message);
            else Debug.LogError(message + "\n" + string.Join("\n", validation.Failures));
        }

        private sealed class ValidationState
        {
            private readonly List<string> failures;
            public ValidationState() : this(0, 0, null) { }
            public ValidationState(int passed, int total, IEnumerable<string> failures)
            {
                Passed = passed;
                Total = total;
                this.failures = failures == null ? new List<string>() : new List<string>(failures);
            }
            public int Passed { get; private set; }
            public int Total { get; private set; }
            public IReadOnlyList<string> Failures => failures;
            public void Check(bool condition, string name)
            {
                Total++;
                if (condition) Passed++;
                else failures.Add(name);
            }
            public void Fail(string name) { failures.Add(name); }
        }
    }
}
