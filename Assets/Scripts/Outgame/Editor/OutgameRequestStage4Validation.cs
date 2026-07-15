using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HATAGONG.GameFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.Outgame.Editor
{
    public static class OutgameRequestStage4Validation
    {
        private const string ScenePath = "Assets/Scenes/OUTGAME_LOBBY.unity";
        private const string PrefabPath = "Assets/Prefabs/Outgame/Requests/OutgameRequestCard.prefab";
        private const string BaseSpritePath = "Assets/Resources/Outgame/base.png";
        private const string InactiveStarPath = "Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star1.png";
        private const string ActiveStarPath = "Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star2.png";
        private const string FontPath = "Assets/Resources/Fonts/Hakgyoansim_JayusiganR SDF.asset";
        private const int ValidationBatchSeed = 20260716;

        private const string PlayRequestedKey = "HATAGONG.Outgame.Stage4.PlayRequested";
        private const string PlayRunningKey = "HATAGONG.Outgame.Stage4.PlayRunning";
        private const string PlayAwaitingExitKey = "HATAGONG.Outgame.Stage4.PlayAwaitingExit";
        private const string PlayPassedKey = "HATAGONG.Outgame.Stage4.PlayPassed";
        private const string PlayTotalKey = "HATAGONG.Outgame.Stage4.PlayTotal";
        private const string PlayFailuresKey = "HATAGONG.Outgame.Stage4.PlayFailures";
        private const string SceneDirtyKey = "HATAGONG.Outgame.Stage4.SceneDirty";
        private const string SceneRootCountKey = "HATAGONG.Outgame.Stage4.SceneRootCount";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/HATAGONG/Outgame/Setup Stage 4 Assets")]
        public static void SetupStage4Assets()
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            if (!string.Equals(scene.path, ScenePath, StringComparison.Ordinal))
                throw new InvalidOperationException("OUTGAME_LOBBY must be the active Scene.");

            Sprite baseSprite = LoadRequired<Sprite>(BaseSpritePath);
            Sprite inactiveStar = LoadRequired<Sprite>(InactiveStarPath);
            Sprite activeStar = LoadRequired<Sprite>(ActiveStarPath);
            TMP_FontAsset font = LoadRequired<TMP_FontAsset>(FontPath);
            EnsureFolder("Assets", "Prefabs");
            EnsureFolder("Assets/Prefabs", "Outgame");
            EnsureFolder("Assets/Prefabs/Outgame", "Requests");

            OutgameRequestCardView prefab = CreateCardPrefab(baseSprite, inactiveStar, activeStar, font);
            CreateScenePopup(prefab);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Outgame][Stage4][Setup] Prefab and OUTGAME_LOBBY popup hierarchy created.");
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 4 EditMode")]
        public static void ValidateEditMode()
        {
            var validation = new ValidationState();
            try
            {
                ValidateSceneAndPrefab(validation);
            }
            catch (Exception exception)
            {
                validation.Fail("Unexpected EditMode exception: " + exception);
            }
            LogResult("EditMode", validation);
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 4 PlayMode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Outgame][Stage4][PlayMode] A Play Mode transition is already active.");
                return;
            }
            Scene scene = EditorSceneManager.GetActiveScene();
            SessionState.SetBool(PlayRequestedKey, true);
            SessionState.SetBool(PlayRunningKey, false);
            SessionState.SetBool(PlayAwaitingExitKey, false);
            SessionState.SetBool(SceneDirtyKey, scene.isDirty);
            SessionState.SetInt(SceneRootCountKey, scene.rootCount);
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
            LogResult("PlayMode", validation);
        }

        private static OutgameRequestCardView CreateCardPrefab(
            Sprite baseSprite,
            Sprite inactiveStar,
            Sprite activeStar,
            TMP_FontAsset font)
        {
            var root = new GameObject("OutgameRequestCard", typeof(RectTransform), typeof(LayoutElement), typeof(OutgameRequestCardView));
            root.layer = LayerMask.NameToLayer("UI");
            RectTransform rootRect = root.GetComponent<RectTransform>();
            SetRect(rootRect, Vector2.zero, new Vector2(390f, 541.85f));
            LayoutElement layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 390f;
            layout.preferredHeight = 541.85f;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;

            Image background = CreateImage("ClipboardBackground", rootRect, Vector2.zero, rootRect.sizeDelta, Color.white);
            background.sprite = baseSprite;
            background.preserveAspect = true;

            TextMeshProUGUI type = CreateText("RequestTypeLabel", rootRect, new Vector2(0f, 225f), new Vector2(330f, 34f), font, 21f, TextAlignmentOptions.Center);
            Image portrait = CreateImage("PortraitPlaceholder", rootRect, new Vector2(-125f, 150f), new Vector2(72f, 72f), new Color32(208, 192, 168, 255));
            TextMeshProUGUI requester = CreateText("RequesterNameLabel", rootRect, new Vector2(35f, 170f), new Vector2(230f, 30f), font, 16f, TextAlignmentOptions.Left);
            TextMeshProUGUI title = CreateText("TitleLabel", rootRect, new Vector2(35f, 132f), new Vector2(230f, 38f), font, 20f, TextAlignmentOptions.Left);
            TextMeshProUGUI difficulty = CreateText("DifficultyLabel", rootRect, new Vector2(-80f, 92f), new Vector2(90f, 24f), font, 15f, TextAlignmentOptions.Left);

            RectTransform starsRoot = CreateRect("DifficultyStars", rootRect, new Vector2(55f, 78f), new Vector2(150f, 30f));
            var stars = new Image[3];
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i] = CreateImage("Star" + (i + 1), starsRoot, new Vector2(-48f + (48f * i), 0f), new Vector2(30f, 29f), Color.white);
                stars[i].sprite = inactiveStar;
                stars[i].preserveAspect = true;
            }

            TextMeshProUGUI descriptionTitle = CreateText("DescriptionTitleLabel", rootRect, new Vector2(0f, 30f), new Vector2(330f, 24f), font, 16f, TextAlignmentOptions.Left);
            TextMeshProUGUI description = CreateText("DescriptionLabel", rootRect, new Vector2(0f, -38f), new Vector2(330f, 94f), font, 14f, TextAlignmentOptions.TopLeft);
            description.textWrappingMode = TextWrappingModes.Normal;
            description.overflowMode = TextOverflowModes.Ellipsis;

            RectTransform effectSlotsRoot = CreateRect("EffectSlots", rootRect, new Vector2(0f, -142f), new Vector2(350f, 72f));
            var slotBackgrounds = new Image[3];
            var slotIcons = new Image[3];
            var slotLabels = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                slotBackgrounds[i] = CreateImage("EffectSlot" + (i + 1), effectSlotsRoot, new Vector2(-116f + (116f * i), 0f), new Vector2(108f, 68f), new Color32(142, 142, 142, 255));
                slotIcons[i] = CreateImage("IconPlaceholder", slotBackgrounds[i].rectTransform, new Vector2(-29f, 0f), new Vector2(34f, 34f), Color.clear);
                slotIcons[i].sprite = null;
                slotLabels[i] = CreateText("EffectNameLabel", slotBackgrounds[i].rectTransform, new Vector2(24f, 0f), new Vector2(62f, 52f), font, 11f, TextAlignmentOptions.Center);
                slotLabels[i].textWrappingMode = TextWrappingModes.Normal;
            }

            Image buttonImage = CreateImage("PerformButton", rootRect, new Vector2(0f, -225f), new Vector2(220f, 48f), new Color32(76, 126, 177, 255));
            Button button = buttonImage.gameObject.AddComponent<Button>();
            button.interactable = false;
            TextMeshProUGUI buttonLabel = CreateText("PerformButtonLabel", buttonImage.rectTransform, Vector2.zero, buttonImage.rectTransform.sizeDelta, font, 18f, TextAlignmentOptions.Center);

            OutgameRequestCardView view = root.GetComponent<OutgameRequestCardView>();
            var serialized = new SerializedObject(view);
            SetReference(serialized, "clipboardBackground", background);
            SetReference(serialized, "portraitPlaceholder", portrait);
            SetReference(serialized, "requestTypeLabel", type);
            SetReference(serialized, "requesterNameLabel", requester);
            SetReference(serialized, "titleLabel", title);
            SetReference(serialized, "difficultyLabel", difficulty);
            SetReferenceArray(serialized, "difficultyStars", stars.Cast<UnityEngine.Object>().ToArray());
            SetReference(serialized, "activeStarSprite", activeStar);
            SetReference(serialized, "inactiveStarSprite", inactiveStar);
            SetReference(serialized, "descriptionTitleLabel", descriptionTitle);
            SetReference(serialized, "descriptionLabel", description);
            SerializedProperty slots = serialized.FindProperty("effectSlots");
            slots.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                slot.FindPropertyRelative("background").objectReferenceValue = slotBackgrounds[i];
                slot.FindPropertyRelative("iconPlaceholder").objectReferenceValue = slotIcons[i];
                slot.FindPropertyRelative("effectNameLabel").objectReferenceValue = slotLabels[i];
            }
            SetReference(serialized, "performButton", button);
            SetReference(serialized, "performButtonLabel", buttonLabel);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            if (saved == null) throw new InvalidOperationException("Failed to save request card Prefab.");
            return saved.GetComponent<OutgameRequestCardView>();
        }

        private static void CreateScenePopup(OutgameRequestCardView prefab)
        {
            GameObject general = GameObject.Find("LobbyCanvas/Outgame_UI_General");
            if (general == null) throw new InvalidOperationException("Outgame_UI_General was not found.");
            Transform existing = general.transform.Find("RequestPopupLayer");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            RectTransform layer = CreateRect("RequestPopupLayer", general.transform as RectTransform, Vector2.zero, Vector2.zero);
            Stretch(layer, Vector2.zero, Vector2.zero);
            Image dim = CreateImage("Dim", layer, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.72f));
            Stretch(dim.rectTransform, Vector2.zero, Vector2.zero);
            dim.raycastTarget = true;

            RectTransform popupRoot = CreateRect("RequestPopupRoot", layer, Vector2.zero, new Vector2(1360f, 2200f));
            RectTransform viewport = CreateRect("RequestViewport", popupRoot, Vector2.zero, Vector2.zero);
            Stretch(viewport, new Vector2(30f, 70f), new Vector2(-30f, -70f));
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("RequestContent", viewport, Vector2.zero, Vector2.zero);
            Stretch(content, Vector2.zero, Vector2.zero);
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(35, 35, 35, 35);
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            OutgameRequestPopupView popup = layer.gameObject.AddComponent<OutgameRequestPopupView>();
            var serialized = new SerializedObject(popup);
            SetReference(serialized, "requestContent", content);
            SetReference(serialized, "cardPrefab", prefab);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            layer.SetAsLastSibling();
            layer.gameObject.SetActive(false);
            EditorUtility.SetDirty(general);
        }

        private static void ValidateSceneAndPrefab(ValidationState validation)
        {
            Scene scene = EditorSceneManager.GetActiveScene();
            validation.Check(string.Equals(scene.path, ScenePath, StringComparison.Ordinal), "OUTGAME_LOBBY is active");
            GameObject general = GameObject.Find("LobbyCanvas/Outgame_UI_General");
            validation.Check(general != null, "Outgame_UI_General exists");
            Transform layer = general == null ? null : general.transform.Find("RequestPopupLayer");
            validation.Check(layer != null, "RequestPopupLayer exists");
            if (layer != null)
            {
                validation.Check(!layer.gameObject.activeSelf, "RequestPopupLayer is inactive by default");
                validation.Check(layer.parent == general.transform, "RequestPopupLayer parent is Outgame_UI_General");
                validation.Check(layer.GetSiblingIndex() == general.transform.childCount - 1, "RequestPopupLayer is the last child");
                validation.Check(layer.Find("Dim") != null, "Dim exists");
                validation.Check(layer.Find("RequestPopupRoot/RequestViewport/RequestContent") != null, "Popup Viewport and Content exist");
                validation.Check(layer.GetComponentsInChildren<Canvas>(true).Length == 0, "No additional Canvas");
                validation.Check(layer.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length == 0, "No additional EventSystem");
                validation.Check(layer.GetComponentsInChildren<ScrollRect>(true).Length <= 1, "No unexpected additional ScrollRect");
                validation.Check(!HasMissingComponent(layer.gameObject), "Scene popup has no Missing Script");
                OutgameRequestPopupView popup = layer.GetComponent<OutgameRequestPopupView>();
                validation.Check(popup != null, "Popup View component exists");
                if (popup != null)
                {
                    var serialized = new SerializedObject(popup);
                    validation.Check(serialized.FindProperty("requestContent").objectReferenceValue != null, "RequestContent reference exists");
                    validation.Check(serialized.FindProperty("cardPrefab").objectReferenceValue != null, "Card Prefab reference exists");
                }
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                validation.Check(prefabRoot != null, "Card Prefab exists");
                if (prefabRoot == null) return;
                validation.Check(prefabRoot.name == "OutgameRequestCard", "Prefab root name");
                validation.Check(!HasMissingComponent(prefabRoot), "Prefab has no Missing Script");
                validation.Check(prefabRoot.GetComponent<OutgameRequestCardView>() != null, "Card View exists");
                string[] required =
                {
                    "ClipboardBackground", "RequestTypeLabel", "PortraitPlaceholder", "RequesterNameLabel",
                    "TitleLabel", "DifficultyLabel", "DifficultyStars/Star1", "DifficultyStars/Star2",
                    "DifficultyStars/Star3", "DescriptionTitleLabel", "DescriptionLabel", "EffectSlots/EffectSlot1",
                    "EffectSlots/EffectSlot2", "EffectSlots/EffectSlot3", "PerformButton/PerformButtonLabel"
                };
                foreach (string path in required)
                    validation.Check(prefabRoot.transform.Find(path) != null, "Prefab path exists: " + path);
                Image background = prefabRoot.transform.Find("ClipboardBackground").GetComponent<Image>();
                validation.Check(background.sprite == LoadRequired<Sprite>(BaseSpritePath), "base.png is directly referenced");
                Image[] stars = prefabRoot.transform.Find("DifficultyStars").GetComponentsInChildren<Image>(true);
                validation.Check(stars.Length == 3, "Three star Images exist");
                validation.Check(stars.All(value => value.sprite == LoadRequired<Sprite>(InactiveStarPath)), "Default stars use inactive Sprite");
                TMP_FontAsset font = LoadRequired<TMP_FontAsset>(FontPath);
                TextMeshProUGUI[] texts = prefabRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
                validation.Check(texts.Length == 10, "Ten TMP labels exist");
                validation.Check(texts.All(value => value.font == font), "Every TMP label uses required font");
                Button button = prefabRoot.transform.Find("PerformButton").GetComponent<Button>();
                validation.Check(!button.interactable, "PerformButton is disabled");
                validation.Check(button.onClick.GetPersistentEventCount() == 0, "PerformButton persistent OnClick count is zero");
            }
            finally
            {
                if (prefabRoot != null) PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static async void RunPlayModeValidationAsync()
        {
            var validation = new ValidationState();
            try
            {
                OutgameRequestSelectionController selection = Resources.FindObjectsOfTypeAll<OutgameRequestSelectionController>()
                    .FirstOrDefault(value => value.gameObject.scene.IsValid());
                if (selection != null) selection.enabled = false;
                OutgameRequestPopupView popup = Resources.FindObjectsOfTypeAll<OutgameRequestPopupView>()
                    .FirstOrDefault(value => value.gameObject.scene.IsValid());
                validation.Check(popup != null, "Popup View exists in Play Mode");
                if (popup == null) throw new InvalidOperationException("Popup View is missing.");
                popup.gameObject.SetActive(true);

                OutgameRequestTableLoadResult loaded = await OutgameRequestTableLoader.LoadAsync();
                validation.Check(loaded.Success, "Actual CSV load succeeds");
                validation.Check(loaded.Errors.Count == 0, "Actual CSV load errors are zero");
                validation.Check(loaded.Catalog != null, "Actual Catalog exists");
                if (loaded.Catalog == null) throw new InvalidOperationException("Catalog is missing.");

                OutgameRequestOfferGenerationResult generated =
                    OutgameRequestOfferGenerator.Generate(loaded.Catalog, ValidationBatchSeed);
                validation.Check(generated.Success, "Actual Batch generation succeeds");
                validation.Check(generated.Batch != null && generated.Batch.Offers.Count == 3, "Actual Batch has three offers");
                popup.Bind(generated.Batch);
                Canvas.ForceUpdateCanvases();
                validation.Check(popup.Cards.Count == 3, "Popup creates three cards");
                validation.Check(popup.transform.Find("Dim").gameObject.activeInHierarchy, "Dim is displayed");
                validation.Check(popup.Cards.All(value => value.BoundOffer != null), "All cards are bound");
                validation.Check(popup.Cards.All(HasCorrectCardRatio), "All cards preserve base ratio");

                foreach (OutgameRequestDefinition definition in loaded.Catalog.Requests)
                {
                    OutgameRequestOffer offer = FindOffer(loaded.Catalog, definition.RequestId);
                    validation.Check(offer != null, definition.RequestId + " offer is available");
                    OutgameRequestCardView card = popup.Cards[0];
                    card.Bind(offer);
                    Canvas.ForceUpdateCanvases();
                    validation.Check(ReferenceEquals(card.BoundOffer, offer), definition.RequestId + " binds exact offer");
                    validation.Check(card.RequestTypeText == (definition.RequestType == RequestType.Sudden ? "특수 의뢰 발생!" : "일반 의뢰 발생!"), definition.RequestId + " type label");
                    validation.Check(card.RequesterNameText == "의뢰주: " + definition.RequesterName, definition.RequestId + " requester");
                    validation.Check(card.TitleText == definition.Title, definition.RequestId + " title");
                    validation.Check(card.DescriptionText == definition.Description, definition.RequestId + " description and newline");
                    validation.Check(CountActiveStars(card) == (int)definition.Difficulty, definition.RequestId + " difficulty stars");
                    validation.Check(card.FilledEffectCount == definition.Effects.Count, definition.RequestId + " filled effect count");
                    validation.Check(EffectsAreLeftAligned(card, definition), definition.RequestId + " effects are left aligned");
                    validation.Check(!card.IsPerformButtonInteractable, definition.RequestId + " PerformButton disabled");
                }

                OutgameRequestCardView rebound = popup.Cards[0];
                OutgameRequestOffer threeEffects = FindOffer(loaded.Catalog, "REQ-S-NORMAL-004");
                OutgameRequestOffer zeroEffects = FindOffer(loaded.Catalog, "REQ-N-EASY-001");
                rebound.Bind(threeEffects);
                rebound.Bind(zeroEffects);
                validation.Check(rebound.FilledEffectCount == 0, "ReBind clears prior filled effects");
                validation.Check(EffectsAreLeftAligned(rebound, zeroEffects.Definition), "ReBind clears prior effect names and icons");
                validation.Check(rebound.TitleText == zeroEffects.Definition.Title, "ReBind clears prior title");

                TextMeshProUGUI[] allTexts = popup.GetComponentsInChildren<TextMeshProUGUI>(true);
                TMP_FontAsset requiredFont = LoadRequired<TMP_FontAsset>(FontPath);
                validation.Check(allTexts.All(value => value.font == requiredFont), "All runtime TMP labels use required font");
                validation.Check(allTexts.All(value => value.text.IndexOf('\uFFFD') < 0 && value.text.IndexOf('\u25A1') < 0), "No replacement square characters");
                validation.Check(popup.Cards.All(CardChildrenStayInside), "All card UI stays inside base area");
                validation.Check(popup.Cards.All(DescriptionAndButtonDoNotOverlap), "Description, effects and button do not overlap");
                validation.Check(popup.transform.parent.name == "Outgame_UI_General", "Popup is independent of LobbyNPC");

                await Task.Delay(1500);
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

        private static OutgameRequestOffer FindOffer(OutgameRequestCatalog catalog, string requestId)
        {
            for (int seed = 0; seed < 256; seed++)
            {
                OutgameRequestOfferGenerationResult generated = OutgameRequestOfferGenerator.Generate(catalog, seed);
                if (generated.Batch == null) continue;
                OutgameRequestOffer offer = generated.Batch.Offers.FirstOrDefault(
                    value => string.Equals(value.Definition.RequestId, requestId, StringComparison.Ordinal));
                if (offer != null) return offer;
            }
            return null;
        }

        private static int CountActiveStars(OutgameRequestCardView card)
        {
            Sprite active = LoadRequired<Sprite>(ActiveStarPath);
            Transform root = card.transform.Find("DifficultyStars");
            return root.GetComponentsInChildren<Image>(true).Count(value => value.sprite == active);
        }

        private static bool EffectsAreLeftAligned(OutgameRequestCardView card, OutgameRequestDefinition definition)
        {
            Transform root = card.transform.Find("EffectSlots");
            for (int i = 0; i < 3; i++)
            {
                Transform slot = root.Find("EffectSlot" + (i + 1));
                Transform icon = slot.Find("IconPlaceholder");
                TextMeshProUGUI label = slot.Find("EffectNameLabel").GetComponent<TextMeshProUGUI>();
                bool shouldFill = i < definition.Effects.Count;
                if (icon.gameObject.activeSelf != shouldFill || label.gameObject.activeSelf != shouldFill) return false;
                if (shouldFill && label.text != definition.Effects[i].EffectName) return false;
                if (!shouldFill && !string.IsNullOrEmpty(label.text)) return false;
                Color32 expectedEmpty = new Color32(142, 142, 142, 255);
                if (!shouldFill && slot.GetComponent<Image>().color != expectedEmpty) return false;
            }
            return true;
        }

        private static bool HasCorrectCardRatio(OutgameRequestCardView card)
        {
            Rect rect = ((RectTransform)card.transform).rect;
            float expected = 1038f / 1442f;
            return Mathf.Abs((rect.width / rect.height) - expected) < 0.002f;
        }

        private static bool CardChildrenStayInside(OutgameRequestCardView card)
        {
            RectTransform root = card.transform as RectTransform;
            Rect rootRect = root.rect;
            foreach (RectTransform child in card.GetComponentsInChildren<RectTransform>(true))
            {
                if (child == root) continue;
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(root, child);
                if (bounds.min.x < rootRect.xMin - 0.5f || bounds.max.x > rootRect.xMax + 0.5f ||
                    bounds.min.y < rootRect.yMin - 0.5f || bounds.max.y > rootRect.yMax + 0.5f) return false;
            }
            return true;
        }

        private static bool DescriptionAndButtonDoNotOverlap(OutgameRequestCardView card)
        {
            RectTransform description = card.transform.Find("DescriptionLabel") as RectTransform;
            RectTransform effects = card.transform.Find("EffectSlots") as RectTransform;
            RectTransform button = card.transform.Find("PerformButton") as RectTransform;
            float descriptionBottom = description.anchoredPosition.y - (description.rect.height * 0.5f);
            float effectsTop = effects.anchoredPosition.y + (effects.rect.height * 0.5f);
            float effectsBottom = effects.anchoredPosition.y - (effects.rect.height * 0.5f);
            float buttonTop = button.anchoredPosition.y + (button.rect.height * 0.5f);
            return descriptionBottom > effectsTop && effectsBottom > buttonTop;
        }

        private static bool HasMissingComponent(GameObject root)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.GetComponents<Component>().Any(component => component == null)) return true;
            return false;
        }

        private static T LoadRequired<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required asset is missing: " + path);
            return asset;
        }

        private static RectTransform CreateRect(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = LayerMask.NameToLayer("UI");
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, position, size);
            return rect;
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI CreateText(
            string name,
            RectTransform parent,
            Vector2 position,
            Vector2 size,
            TMP_FontAsset font,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = new Color32(37, 32, 27, 255);
            text.alignment = alignment;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = Mathf.Max(8f, fontSize * 0.65f);
            text.fontSizeMax = fontSize;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetReference(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            serialized.FindProperty(name).objectReferenceValue = value;
        }

        private static void SetReferenceArray(SerializedObject serialized, string name, UnityEngine.Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
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

        private static void LogResult(string mode, ValidationState validation)
        {
            string message = $"[Outgame][Stage4][{mode}] result={validation.Passed}/{validation.Total}, failures={validation.Failures.Count}";
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
