#if UNITY_EDITOR
using HATAGONG.GameFlow;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.GameFlowEditor
{
    public static class IngameHudPersistenceFix
    {
        private const string ScenePath = "Assets/Scenes/INGAME.unity";
        private const string HakgyoPath = "Assets/Resources/Fonts/Hakgyoansim_JayusiganR SDF.asset";
        private const string JuaPath = "Assets/Resources/Fonts/Jua-Regular SDF.asset";
        private const string PhaseOnPath = "Assets/Resources/Ingame/ICON/PhaseICON/Img_icon_phaseOn.png";
        private const string PhaseOffPath = "Assets/Resources/Ingame/ICON/PhaseICON/Img_icon_phaseOff.png";
        private const string ItemButtonPath = "Assets/Resources/Ingame/UI/Button/Img_button_item.png";
        private const string OptionButtonPath = "Assets/Resources/Ingame/UI/Button/Img_button_option 1.png";

        [MenuItem("Tools/HATAGONG/INGAME/Validate HUD Persistence Fix")]
        public static void ValidateHudPersistenceFix()
        {
            if (!TryGetLoadedScene(out Scene scene)) return;

            int differences = InspectOrApply(scene, false);
            Debug.Log(differences == 0
                ? "[GameFlow] INGAME HUD persistence validation passed. No scene changes were made."
                : $"[GameFlow] INGAME HUD persistence validation found {differences} difference(s). No scene changes were made.");
        }

        [MenuItem("Tools/HATAGONG/INGAME/Apply HUD Persistence Fix")]
        public static void ApplyHudPersistenceFix()
        {
            if (!TryGetLoadedScene(out Scene scene)) return;

            int differences = InspectOrApply(scene, true);
            if (differences == 0)
            {
                Debug.Log("[GameFlow] INGAME HUD persistence fix is already up to date. Scene was not dirtied or saved.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[GameFlow] Applied {differences} INGAME HUD persistence fix(es). Scene was not saved; press Ctrl+S after review.");
        }

        private static bool TryGetLoadedScene(out Scene scene)
        {
            scene = default;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[GameFlow] INGAME HUD persistence tools are Edit Mode only.");
                return false;
            }

            scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[GameFlow] Load Assets/Scenes/INGAME.unity before using this tool.");
                return false;
            }

            return true;
        }

        private static int InspectOrApply(Scene scene, bool apply)
        {
            int differences = 0;

            TMP_FontAsset hakgyo = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HakgyoPath);
            TMP_FontAsset jua = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JuaPath);
            Sprite phaseOn = AssetDatabase.LoadAssetAtPath<Sprite>(PhaseOnPath);
            Sprite phaseOff = AssetDatabase.LoadAssetAtPath<Sprite>(PhaseOffPath);
            Sprite itemButton = AssetDatabase.LoadAssetAtPath<Sprite>(ItemButtonPath);
            Sprite optionButton = AssetDatabase.LoadAssetAtPath<Sprite>(OptionButtonPath);
            if (!hakgyo || !jua || !phaseOn || !phaseOff || !itemButton || !optionButton)
                throw new System.InvalidOperationException("Required INGAME HUD Font/Sprite asset is missing; no references were changed.");

            differences += Text("Canvas/Game_UI_General/Top_HUD/Score_Content/Score_Title", hakgyo, "점 수", 80f, false, apply, new Vector2(0f, -82f));
            differences += Text("Canvas/Game_UI_General/Top_HUD/Time_Content/Time_Title", hakgyo, "시  간", 80f, false, apply, new Vector2(0f, -82f));
            differences += Text("Canvas/Game_UI_General/Top_HUD/Time_Content/Time_Value", jua, null, 112f, false, apply);
            differences += Text("Canvas/Game_UI_General/Top_HUD/Request_Content/Request_Text", hakgyo, null, 72f, false, apply);
            differences += Text("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_Text", hakgyo, null, 72f, false, apply);
            differences += Text("Canvas/Game_UI_General/Top_HUD/Score_Content/Score_Value", hakgyo, null, 72f, true, apply);

            foreach (string valueName in new[] { "Item_Value01", "Item_Value02", "Item_Value03" })
                differences += Text("Canvas/Game_UI_General/Bottom_HUD/Item_Content/" + valueName, hakgyo, null, 34f, true, apply, null, false);

            TextMeshProUGUI phaseTitle = Find("Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_TitleArea/Phase_Text").GetComponent<TextMeshProUGUI>();
            differences += Text(phaseTitle, hakgyo, "PHASE 1", 46f, true, apply, null);
            PhaseHUDPresenter presenter = Find("Canvas/Game_UI_General/Top_HUD/Phase_Content").GetComponent<PhaseHUDPresenter>();
            SerializedObject presenterObject = new SerializedObject(presenter);
            differences += SetReference(presenterObject.FindProperty("phaseText"), phaseTitle, apply);
            differences += SetReference(presenterObject.FindProperty("activeDotSprite"), phaseOn, apply);
            differences += SetReference(presenterObject.FindProperty("inactiveDotSprite"), phaseOff, apply);
            differences += SetDescriptions(presenterObject.FindProperty("descriptions"), apply);
            if (apply) presenterObject.ApplyModifiedPropertiesWithoutUndo();

            foreach (string buttonName in new[] { "Item_Button01", "Item_Button02", "Item_Button03" })
                differences += SetImageSprite(FindByName(scene, buttonName), itemButton, apply);
            GameObject settings = Find("Canvas/Game_UI_Settings/Settings_Button");
            differences += SetImageSprite(settings, optionButton, apply);
            FixedImageSprite fixedImage = settings.GetComponent<FixedImageSprite>();
            if (fixedImage)
            {
                SerializedObject fixedObject = new SerializedObject(fixedImage);
                differences += SetReference(fixedObject.FindProperty("target"), settings.GetComponent<Image>(), apply);
                differences += SetReference(fixedObject.FindProperty("fixedSprite"), optionButton, apply);
                if (apply) fixedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return differences;
        }

        private static int Text(string path, TMP_FontAsset font, string value, float size, bool keepAutoSize, bool apply, Vector2? position = null, bool required = true)
        {
            GameObject gameObject = Find(path, required);
            return gameObject ? Text(gameObject.GetComponent<TextMeshProUGUI>(), font, value, size, keepAutoSize, apply, position) : 0;
        }

        private static int Text(TextMeshProUGUI text, TMP_FontAsset font, string value, float size, bool keepAutoSize, bool apply, Vector2? position)
        {
            int differences = 0;
            if (text.font != font) { differences++; if (apply) text.font = font; }
            if (value != null && text.text != value) { differences++; if (apply) text.text = value; }
            if (text.enableAutoSizing != keepAutoSize) { differences++; if (apply) text.enableAutoSizing = keepAutoSize; }
            if (!Mathf.Approximately(text.fontSize, size)) { differences++; if (apply) text.fontSize = size; }
            if (!Mathf.Approximately(text.fontSizeMax, size)) { differences++; if (apply) text.fontSizeMax = size; }
            if (position.HasValue && text.rectTransform.anchoredPosition != position.Value)
            {
                differences++;
                if (apply) text.rectTransform.anchoredPosition = position.Value;
            }
            return differences;
        }

        private static int SetReference(SerializedProperty property, Object value, bool apply)
        {
            if (property.objectReferenceValue == value) return 0;
            if (apply) property.objectReferenceValue = value;
            return 1;
        }

        private static int SetDescriptions(SerializedProperty descriptions, bool apply)
        {
            string[] values = { "철 거", "도 포", "시 공" };
            int differences = descriptions.arraySize == values.Length ? 0 : 1;
            if (apply && descriptions.arraySize != values.Length) descriptions.arraySize = values.Length;
            int count = Mathf.Min(descriptions.arraySize, values.Length);
            for (int i = 0; i < count; i++)
            {
                SerializedProperty element = descriptions.GetArrayElementAtIndex(i);
                if (element.stringValue == values[i]) continue;
                differences++;
                if (apply) element.stringValue = values[i];
            }
            return differences;
        }

        private static int SetImageSprite(GameObject gameObject, Sprite sprite, bool apply)
        {
            if (!gameObject || !sprite) return 0;
            Image image = gameObject.GetComponent<Image>();
            if (image.sprite == sprite) return 0;
            if (apply) image.sprite = sprite;
            return 1;
        }

        private static GameObject Find(string path, bool required = true)
        {
            GameObject result = GameObject.Find(path);
            if (!result && required) throw new System.InvalidOperationException("Required INGAME HUD object missing: " + path);
            return result;
        }

        private static GameObject FindByName(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child.gameObject;
            }
            throw new System.InvalidOperationException("Required INGAME HUD object missing: " + name);
        }
    }
}
#endif
