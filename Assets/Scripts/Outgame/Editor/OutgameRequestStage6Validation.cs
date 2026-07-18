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
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.Outgame.Editor
{
    public static class OutgameRequestStage6Validation
    {
        public const int ExpectedEditModeAssertions = 35;
        public const int ExpectedPlayModeAssertions = 58;

        private const string OutgameScenePath = "Assets/Scenes/OUTGAME_LOBBY.unity";
        private const string IngameScenePath = "Assets/Scenes/INGAME.unity";
        private const string GeneralPath = "LobbyCanvas/Outgame_UI_General";
        private const string PopupRelativePath = "RequestPopupLayer";
        private const string QuestPath = GeneralPath + "/ContentLayer/LobbyNPC/QuestIndicator";
        private const string CardPrefabPath = "Assets/Prefabs/Outgame/Requests/OutgameRequestCard.prefab";

        private const string RequestedKey = "HATAGONG.Outgame.Stage6.PlayRequested";
        private const string RunningKey = "HATAGONG.Outgame.Stage6.PlayRunning";
        private const string AwaitingExitKey = "HATAGONG.Outgame.Stage6.PlayAwaitingExit";
        private const string PassedKey = "HATAGONG.Outgame.Stage6.PlayPassed";
        private const string TotalKey = "HATAGONG.Outgame.Stage6.PlayTotal";
        private const string FailuresKey = "HATAGONG.Outgame.Stage6.PlayFailures";
        private const string DirtyKey = "HATAGONG.Outgame.Stage6.SceneDirty";
        private const string RootCountKey = "HATAGONG.Outgame.Stage6.SceneRootCount";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 6 EditMode")]
        public static void ValidateEditMode()
        {
            var validation = new ValidationState();
            try { RunEditModeValidation(validation); }
            catch (Exception exception) { validation.Fail("Unexpected EditMode exception: " + exception); }
            LogResult("EditMode", validation, ExpectedEditModeAssertions);
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 6 PlayMode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Outgame][Stage6][PlayMode] A Play Mode transition is already active.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            SessionState.SetBool(RequestedKey, true);
            SessionState.SetBool(RunningKey, false);
            SessionState.SetBool(AwaitingExitKey, false);
            SessionState.SetBool(DirtyKey, scene.isDirty);
            SessionState.SetInt(RootCountKey, scene.rootCount);
            FocusGameView();
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(RequestedKey, false) &&
                !SessionState.GetBool(RunningKey, false))
            {
                SessionState.SetBool(RunningKey, true);
                RunPlayModeValidationAsync();
                return;
            }

            if (state != PlayModeStateChange.EnteredEditMode ||
                !SessionState.GetBool(AwaitingExitKey, false)) return;

            var validation = new ValidationState(
                SessionState.GetInt(PassedKey, 0),
                SessionState.GetInt(TotalKey, 0),
                DeserializeFailures(SessionState.GetString(FailuresKey, string.Empty)));
            Scene scene = EditorSceneManager.GetActiveScene();
            validation.Check(scene.path == OutgameScenePath, "OUTGAME_LOBBY is restored after Play Mode");
            validation.Check(scene.isDirty == SessionState.GetBool(DirtyKey, false), "OUTGAME dirty state is unchanged");
            validation.Check(scene.rootCount == SessionState.GetInt(RootCountKey, -1), "OUTGAME root count is unchanged");
            ClearSessionState();
            LogResult("PlayMode", validation, ExpectedPlayModeAssertions);
        }

        private static void RunEditModeValidation(ValidationState validation)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            validation.Check(scene.path == OutgameScenePath, "OUTGAME_LOBBY is active");
            validation.Check(EditorBuildSettings.scenes.Any(value => value.enabled && value.path == IngameScenePath), "INGAME is enabled in Build Settings");
            validation.Check(typeof(OutgameRequestRunSelection).IsSealed, "RunSelection is sealed");

            string[] propertyNames =
            {
                "RequestId", "Difficulty", "RequestType", "PermanentSeed",
                "Phase1Seed", "Phase3Seed", "Phase3ImageKey"
            };
            foreach (string propertyName in propertyNames)
            {
                PropertyInfo property = typeof(OutgameRequestRunSelection).GetProperty(propertyName);
                validation.Check(property != null && property.CanRead && !property.CanWrite, propertyName + " is read-only");
            }

            OutgameRequestSelectionStore.Clear();
            validation.Check(!OutgameRequestSelectionStore.HasPending, "Store initially has no Pending selection");
            OutgameRequestOffer fixtureOffer = CreateOfferFixture();
            OutgameRequestSelectionStore.SetPending(fixtureOffer);
            validation.Check(OutgameRequestSelectionStore.HasPending, "SetPending creates a selection");
            validation.Check(OutgameRequestSelectionStore.TryGetPending(out OutgameRequestRunSelection selection), "TryGetPending succeeds");
            validation.Check(SelectionMatchesOffer(selection, fixtureOffer), "Snapshot exactly matches its Offer");
            validation.Check(selection != null && selection.GetType() != fixtureOffer.GetType(), "Store contains a Snapshot rather than an Offer");
            OutgameRequestSelectionStore.Clear();
            validation.Check(!OutgameRequestSelectionStore.HasPending, "Clear removes Pending selection");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            validation.Check(prefab != null, "Card Prefab exists");
            Transform performTransform = prefab == null ? null : prefab.transform.Find("PerformButton");
            Button performButton = performTransform == null ? null : performTransform.GetComponent<Button>();
            validation.Check(performButton != null, "PerformButton exists");
            validation.Check(performButton != null && !performButton.interactable, "PerformButton default is disabled");
            validation.Check(performButton != null && performButton.onClick.GetPersistentEventCount() == 0, "PerformButton persistent OnClick is zero");
            Graphic targetGraphic = performButton == null ? null : performButton.targetGraphic;
            validation.Check(targetGraphic != null, "PerformButton targetGraphic exists");
            validation.Check(targetGraphic is Image, "PerformButton targetGraphic is its background Image");
            validation.Check(targetGraphic != null && targetGraphic.raycastTarget, "PerformButton targetGraphic raycast is on");
            TextMeshProUGUI performLabel = performTransform == null ? null : performTransform.Find("PerformButtonLabel")?.GetComponent<TextMeshProUGUI>();
            validation.Check(performLabel != null && !performLabel.raycastTarget, "PerformButton Label raycast remains off");
            validation.Check(typeof(OutgameRequestCardView).GetProperty("BoundOffer") != null, "Card exposes BoundOffer");
            validation.Check(typeof(OutgameRequestCardView).GetEvent("PerformRequested") != null, "Card exposes PerformRequested");
            validation.Check(typeof(OutgameRequestCardView).GetMethod("SetPerformInteractable") != null, "Card exposes SetPerformInteractable");
            validation.Check(typeof(OutgameRequestCarouselController).GetProperty("CurrentPageIndex") != null, "Carousel exposes CurrentPageIndex");
            validation.Check(typeof(OutgameRequestCarouselController).GetEvent("PageChanged") != null, "Carousel exposes PageChanged");
            validation.Check(typeof(OutgameRequestSelectionController).GetProperty("SceneLoadRequestCount") != null, "Selection exposes SceneLoadRequestCount");
            validation.Check(typeof(OutgameRequestSelectionController).GetProperty("IsTransitionRequested") != null, "Selection exposes transition guard state");

            GameObject general = GameObject.Find(GeneralPath);
            validation.Check(general != null && general.GetComponent<OutgameRequestSelectionController>() != null, "Scene Selection Controller exists");
            Transform popup = general == null ? null : general.transform.Find(PopupRelativePath);
            validation.Check(popup != null && !popup.gameObject.activeSelf, "Popup is inactive by default");
            validation.Check(popup != null && !HasMissingComponent(popup.gameObject), "Popup has no Missing Script");
            validation.Check(prefab != null && !HasMissingComponent(prefab), "Card Prefab has no Missing Script");
        }

        private static async void RunPlayModeValidationAsync()
        {
            var validation = new ValidationState();
            UnityAction<Scene, LoadSceneMode> sceneLoaded = null;
            var ingameLoaded = new TaskCompletionSource<Scene>();
            sceneLoaded = (scene, mode) =>
            {
                if (scene.name == "INGAME") ingameLoaded.TrySetResult(scene);
            };
            SceneManager.sceneLoaded += sceneLoaded;

            try
            {
                OutgameRequestSelectionController controller = Resources.FindObjectsOfTypeAll<OutgameRequestSelectionController>()
                    .FirstOrDefault(value => value.gameObject.scene.IsValid());
                validation.Check(controller != null, "Selection Controller exists in Play Mode");
                if (controller == null) throw new InvalidOperationException("Selection Controller is missing.");

                OutgameRequestPopupView popup = Resources.FindObjectsOfTypeAll<OutgameRequestPopupView>()
                    .First(value => value.gameObject.scene.IsValid());
                validation.Check(!popup.gameObject.activeSelf, "Popup starts closed");
                validation.Check(!OutgameRequestSelectionStore.HasPending, "Store starts empty");

                float deadline = Time.realtimeSinceStartup + 8f;
                while (!controller.IsReady && Time.realtimeSinceStartup < deadline) await Task.Yield();
                validation.Check(controller.IsReady, "Selection becomes ready");
                validation.Check(popup.Cards.Count == 3, "Popup owns three cards");
                validation.Check(popup.Cards.All(value => !value.IsPerformButtonInteractable), "All PerformButtons are disabled while closed");

                Button quest = GameObject.Find(QuestPath).GetComponent<Button>();
                quest.onClick.Invoke();
                await AwaitEditorUpdate();
                await AwaitEditorUpdate();
                OutgameRequestCarouselController carousel = popup.GetComponentInChildren<OutgameRequestCarouselController>(true);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(carousel.Content);
                Vector2 cardSize = carousel.CardDisplaySize;
                float widthRatio = cardSize.x / carousel.Viewport.rect.width;
                validation.Check(widthRatio >= 0.88f && widthRatio <= 0.92f, "Card width is 88-92 percent of Viewport");
                validation.Check(Mathf.Abs((cardSize.y / cardSize.x) - (1512f / 1015f)) <= 0.001f, "Card preserves Img_questui aspect ratio");
                validation.Check(popup.Cards.All(value => Vector2.Distance(value.DisplaySize, cardSize) <= 0.1f), "All three cards have identical display size");
                validation.Check(popup.Cards.All(value => value.GetComponentsInChildren<Transform>(true).All(transform => transform.localScale == Vector3.one)), "Every card Transform scale is one");
                validation.Check(Mathf.Abs(carousel.Content.rect.width - (carousel.Viewport.rect.width * 3f)) <= 0.1f, "Content width equals three Viewport pages");
                validation.Check(CardStrideMatchesViewport(popup, carousel), "Card center stride equals Viewport width");
                validation.Check(CardsFitVertically(popup, carousel), "Cards have no vertical clipping");
                validation.Check(PageIndicatorDoesNotOverlapCard(popup, carousel), "PageIndicator does not overlap cards");
                validation.Check(InternalLayoutIsExpanded(popup.Cards[0]), "Text, effect slots and PerformButton use enlarged card area");
                validation.Check(cardSize.x >= 1260f && cardSize.x <= 1320f && cardSize.y >= 1750f && cardSize.y <= 1835f, "1440x2560 reference card size is in target range");

                validation.Check(popup.gameObject.activeSelf, "Quest opens Popup");
                validation.Check(OnlyPageIsInteractable(popup, 0), "Only Page 0 PerformButton is enabled");
                carousel.SnapToPage(1);
                validation.Check(carousel.CurrentPageIndex == 1, "Carousel moves to Page 1");
                validation.Check(OnlyPageIsInteractable(popup, 1), "Only Page 1 PerformButton is enabled");
                carousel.SnapToPage(2);
                validation.Check(carousel.CurrentPageIndex == 2, "Carousel moves to Page 2");
                validation.Check(OnlyPageIsInteractable(popup, 2), "Only Page 2 PerformButton is enabled");

                Button dim = popup.transform.Find("Dim").GetComponent<Button>();
                dim.onClick.Invoke();
                validation.Check(!popup.gameObject.activeSelf, "Dim closes Popup");
                validation.Check(popup.Cards.All(value => !value.IsPerformButtonInteractable), "Closing disables all PerformButtons");
                quest.onClick.Invoke();
                await AwaitEditorUpdate();
                await AwaitEditorUpdate();
                validation.Check(popup.gameObject.activeSelf, "Quest reopens Popup");
                validation.Check(carousel.CurrentPageIndex == 0, "Reopen resets Page 0");
                validation.Check(OnlyPageIsInteractable(popup, 0), "Reopen enables only Page 0");

                OutgameRequestOfferBatch duplicateBatch = CreateDuplicateBatch();
                validation.Check(duplicateBatch != null && duplicateBatch.Offers.Count == 3, "One-request Fixture creates three offers");
                var duplicateSelections = new List<OutgameRequestRunSelection>();
                for (int i = 0; i < duplicateBatch.Offers.Count; i++)
                {
                    OutgameRequestSelectionStore.SetPending(duplicateBatch.Offers[i]);
                    OutgameRequestSelectionStore.TryGetPending(out OutgameRequestRunSelection duplicateSelection);
                    duplicateSelections.Add(duplicateSelection);
                }
                validation.Check(SelectionMatchesOffer(duplicateSelections[0], duplicateBatch.Offers[0]), "Duplicate card 0 Snapshot is exact");
                validation.Check(SelectionMatchesOffer(duplicateSelections[1], duplicateBatch.Offers[1]), "Duplicate card 1 Snapshot is exact");
                validation.Check(SelectionMatchesOffer(duplicateSelections[2], duplicateBatch.Offers[2]), "Duplicate card 2 Snapshot is exact");
                validation.Check(SelectionsEqual(duplicateSelections), "Duplicate cards create identical Snapshots");
                OutgameRequestSelectionStore.Clear();
                validation.Check(!OutgameRequestSelectionStore.HasPending, "Fixture Pending is cleared before production selection");

                OutgameRequestCardView selectedCard = popup.Cards[0];
                OutgameRequestOffer expectedOffer = selectedCard.BoundOffer;
                validation.Check(expectedOffer != null, "Selected card has its exact bound Offer");
                int performEvents = 0;
                selectedCard.PerformRequested += value => performEvents++;
                selectedCard.Bind(expectedOffer);
                selectedCard.SetPerformInteractable(true);
                Button perform = selectedCard.transform.Find("PerformButton").GetComponent<Button>();
                validation.Check(perform.targetGraphic != null, "Runtime PerformButton targetGraphic exists");
                validation.Check(perform.targetGraphic != null && perform.targetGraphic.raycastTarget, "Runtime PerformButton targetGraphic raycast is on");
                validation.Check(perform.interactable, "Current Page PerformButton is interactable before click");
                validation.Check(popup.Cards.Skip(1).All(value => !value.IsPerformButtonInteractable), "Other Page PerformButtons remain disabled");
                RaycastClickResult click = ClickThroughRaycaster(perform);
                validation.Check(click.TopTarget == perform.targetGraphic.gameObject, "PerformButton background Image is the top Raycast target");
                validation.Check(click.ClickHandler == perform.gameObject, "Actual Raycast resolves to the PerformButton hierarchy");
                validation.Check(performEvents == 1, "Actual Pointer click raises exactly one PerformRequested event");
                validation.Check(controller.IsTransitionRequested, "Transition guard is set");
                validation.Check(controller.SceneLoadRequestCount == 1, "LoadSceneAsync is requested exactly once");
                validation.Check(OutgameRequestSelectionStore.HasPending, "Production selection creates Pending");
                OutgameRequestSelectionStore.TryGetPending(out OutgameRequestRunSelection pendingBeforeLoad);
                validation.Check(SelectionMatchesOffer(pendingBeforeLoad, expectedOffer), "Pending exactly matches the clicked Offer");
                validation.Check(popup.Cards.All(value => !value.IsPerformButtonInteractable), "Selection disables every PerformButton");
                validation.Check(!quest.interactable, "Selection disables QuestIndicator input");
                validation.Check(!dim.interactable, "Selection disables Dim input");

                Task completed = await Task.WhenAny(ingameLoaded.Task, Task.Delay(8000));
                validation.Check(completed == ingameLoaded.Task, "INGAME load completes before timeout");
                if (completed != ingameLoaded.Task) throw new TimeoutException("INGAME did not load.");
                Scene ingame = await ingameLoaded.Task;
                await Task.Yield();
                validation.Check(SceneManager.GetActiveScene().name == "INGAME", "INGAME becomes the active Scene");
                GameSessionController session = Resources.FindObjectsOfTypeAll<GameSessionController>()
                    .FirstOrDefault(value => value.gameObject.scene == ingame);
                validation.Check(session != null && ContextMatchesSelection(session.RunContext, pendingBeforeLoad), "INGAME Context copies every Pending field");
                validation.Check(!OutgameRequestSelectionStore.HasPending, "GameSession consumes Pending after Context creation");
                validation.Check(!SceneManager.GetSceneByName("OUTGAME_LOBBY").isLoaded, "OUTGAME_LOBBY is unloaded");
                validation.Check(Resources.FindObjectsOfTypeAll<OutgameRequestSelectionController>().All(value => !value.gameObject.scene.IsValid()), "OUTGAME Selection Controller is removed");
                validation.Check(ingame.IsValid() && ingame.isLoaded && ingame.rootCount > 0, "INGAME Scene is valid and loaded");
            }
            catch (Exception exception)
            {
                validation.Fail("Unexpected PlayMode exception: " + exception);
            }
            finally
            {
                SceneManager.sceneLoaded -= sceneLoaded;
                SessionState.SetInt(PassedKey, validation.Passed);
                SessionState.SetInt(TotalKey, validation.Total);
                SessionState.SetString(FailuresKey, SerializeFailures(validation.Failures));
                SessionState.SetBool(AwaitingExitKey, true);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        private static bool OnlyPageIsInteractable(OutgameRequestPopupView popup, int page)
        {
            if (popup.Cards.Count != 3) return false;
            for (int i = 0; i < popup.Cards.Count; i++)
                if (popup.Cards[i].IsPerformButtonInteractable != (i == page)) return false;
            return true;
        }

        private static RaycastClickResult ClickThroughRaycaster(Button button)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || button == null) return new RaycastClickResult(null, null);
            RectTransform rect = button.transform as RectTransform;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = position,
                pressPosition = position,
                eligibleForClick = true
            };
            var raycasts = new List<RaycastResult>();
            eventSystem.RaycastAll(data, raycasts);
            GameObject topTarget = raycasts.Count == 0 ? null : raycasts[0].gameObject;
            if (topTarget == null) return new RaycastClickResult(null, null);
            data.pointerCurrentRaycast = raycasts[0];
            data.pointerPressRaycast = raycasts[0];
            GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(topTarget);
            GameObject pressHandler = ExecuteEvents.ExecuteHierarchy(topTarget, data, ExecuteEvents.pointerDownHandler);
            data.pointerPress = pressHandler;
            data.rawPointerPress = topTarget;
            if (pressHandler != null) ExecuteEvents.Execute(pressHandler, data, ExecuteEvents.pointerUpHandler);
            if (clickHandler != null) ExecuteEvents.Execute(clickHandler, data, ExecuteEvents.pointerClickHandler);
            return new RaycastClickResult(topTarget, clickHandler);
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

        private sealed class RaycastClickResult
        {
            public RaycastClickResult(GameObject topTarget, GameObject clickHandler) { TopTarget = topTarget; ClickHandler = clickHandler; }
            public GameObject TopTarget { get; }
            public GameObject ClickHandler { get; }
        }

        private static bool CardStrideMatchesViewport(OutgameRequestPopupView popup, OutgameRequestCarouselController carousel)
        {
            if (popup.Cards.Count != 3) return false;
            float expected = carousel.Viewport.rect.width;
            float first = ((RectTransform)popup.Cards[0].transform).anchoredPosition.x;
            float second = ((RectTransform)popup.Cards[1].transform).anchoredPosition.x;
            float third = ((RectTransform)popup.Cards[2].transform).anchoredPosition.x;
            return Mathf.Abs((second - first) - expected) <= 0.1f &&
                Mathf.Abs((third - second) - expected) <= 0.1f;
        }

        private static bool CardsFitVertically(OutgameRequestPopupView popup, OutgameRequestCarouselController carousel)
        {
            var viewportCorners = new Vector3[4];
            carousel.Viewport.GetWorldCorners(viewportCorners);
            foreach (OutgameRequestCardView card in popup.Cards)
            {
                var cardCorners = new Vector3[4];
                ((RectTransform)card.transform).GetWorldCorners(cardCorners);
                if (cardCorners[0].y < viewportCorners[0].y - 0.1f ||
                    cardCorners[1].y > viewportCorners[1].y + 0.1f) return false;
            }
            return true;
        }

        private static bool PageIndicatorDoesNotOverlapCard(OutgameRequestPopupView popup, OutgameRequestCarouselController carousel)
        {
            RectTransform indicator = popup.transform.Find("RequestPopupRoot/PageIndicator") as RectTransform;
            if (indicator == null || popup.Cards.Count == 0) return false;
            var indicatorCorners = new Vector3[4];
            var cardCorners = new Vector3[4];
            indicator.GetWorldCorners(indicatorCorners);
            ((RectTransform)popup.Cards[0].transform).GetWorldCorners(cardCorners);
            return indicatorCorners[1].y < cardCorners[0].y;
        }

        private static bool InternalLayoutIsExpanded(OutgameRequestCardView card)
        {
            RectTransform effects = card.transform.Find("EffectSlots") as RectTransform;
            RectTransform perform = card.transform.Find("PerformButton") as RectTransform;
            TextMeshProUGUI title = card.transform.Find("TitleLabel").GetComponent<TextMeshProUGUI>();
            float cardWidth = card.DisplaySize.x;
            float layoutScale = cardWidth / 390f;
            return effects != null && effects.rect.width >= cardWidth * 0.84f &&
                perform != null && perform.rect.width >= cardWidth * 0.63f &&
                title != null && title.fontSize >= 19f * layoutScale;
        }

        private static bool SelectionMatchesOffer(OutgameRequestRunSelection selection, OutgameRequestOffer offer)
        {
            return selection != null && offer != null &&
                selection.RequestId == offer.Definition.RequestId &&
                selection.Difficulty == offer.Definition.Difficulty &&
                selection.RequestType == offer.Definition.RequestType &&
                selection.PermanentSeed == offer.Definition.PermanentSeed &&
                selection.Phase1Seed == offer.Phase1Seed &&
                selection.Phase3Seed == offer.Phase3Seed &&
                selection.Phase3ImageKey == offer.Phase3ImageKey;
        }

        private static bool ContextMatchesSelection(GameRunContext context, OutgameRequestRunSelection selection)
        {
            return selection != null && context.IsValid && context.HasSelectedRequest &&
                context.RequestId == selection.RequestId && context.Difficulty == selection.Difficulty &&
                context.RequestType == selection.RequestType && context.PermanentSeed == selection.PermanentSeed &&
                context.Phase1Seed == selection.Phase1Seed && context.Phase3Seed == selection.Phase3Seed &&
                context.Phase3ImageKey == selection.Phase3ImageKey;
        }

        private static bool SelectionsEqual(IReadOnlyList<OutgameRequestRunSelection> values)
        {
            if (values == null || values.Count != 3 || values[0] == null) return false;
            OutgameRequestRunSelection first = values[0];
            return values.All(value => value != null &&
                value.RequestId == first.RequestId && value.Difficulty == first.Difficulty &&
                value.RequestType == first.RequestType && value.PermanentSeed == first.PermanentSeed &&
                value.Phase1Seed == first.Phase1Seed && value.Phase3Seed == first.Phase3Seed &&
                value.Phase3ImageKey == first.Phase3ImageKey);
        }

        private static OutgameRequestOffer CreateOfferFixture()
        {
            return CreateDuplicateBatch().Offers[0];
        }

        private static OutgameRequestOfferBatch CreateDuplicateBatch()
        {
            var definition = new OutgameRequestDefinition(
                "STAGE6_FIXTURE", true, RequestType.Sudden, GameDifficulty.Hard, 860001,
                861001, 863001, "Img_bigtiles1",
                "Fixture", "fixture", "Fixture", "Fixture", Array.Empty<OutgameRequestEffectDefinition>());
            ConstructorInfo constructor = typeof(OutgameRequestCatalog).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new[] { typeof(IEnumerable<OutgameRequestDefinition>), typeof(IEnumerable<OutgameRequestEffectDefinition>) }, null);
            var catalog = (OutgameRequestCatalog)constructor.Invoke(new object[]
            {
                new[] { definition }, Array.Empty<OutgameRequestEffectDefinition>()
            });
            return OutgameRequestOfferGenerator.Generate(catalog, 20260716).Batch;
        }

        private static bool HasMissingComponent(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.GetComponents<Component>().Any(component => component == null)) return true;
            return false;
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

        private static void ClearSessionState()
        {
            SessionState.EraseBool(RequestedKey);
            SessionState.EraseBool(RunningKey);
            SessionState.EraseBool(AwaitingExitKey);
            SessionState.EraseInt(PassedKey);
            SessionState.EraseInt(TotalKey);
            SessionState.EraseString(FailuresKey);
            SessionState.EraseBool(DirtyKey);
            SessionState.EraseInt(RootCountKey);
        }

        private static void LogResult(string mode, ValidationState validation, int expected)
        {
            if (validation.Total != expected)
                validation.Fail($"Assertion total mismatch: expected={expected}, actual={validation.Total}");
            string message = $"[Outgame][Stage6][{mode}] result={validation.Passed}/{validation.Total}, expected={expected}, failures={validation.Failures.Count}";
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
