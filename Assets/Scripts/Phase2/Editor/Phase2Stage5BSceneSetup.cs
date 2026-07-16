#if UNITY_EDITOR
using System;
using HATAGONG.GameFlow;
using HATAGONG.Phase1;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.Phase2.Editor
{
    public static class Phase2Stage5BSceneSetup
    {
        private const string ScenePath = "Assets/Scenes/INGAME.unity";
        private const string MaterialPath = "Assets/Materials/Phase2/Phase2BlackCoverMask.mat";

        [MenuItem("Tools/HATAGONG/Phase2/Setup Stage 5B Scene Integration")]
        public static void Setup()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject middle = Require("Canvas/Game_UI_General/Middle_GamePanel");
            GameObject phase1Root = Require("Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot");
            GameObject general = Require("Canvas/Game_UI_General");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material || !material.shader || material.shader.name != Phase2MaskPresenter.ShaderName)
                throw new InvalidOperationException("Required Phase 2 black-cover Material/Shader asset is missing or invalid.");

            GameObject phase2Root = FindOrCreate(middle, "Phase2Root");
            phase2Root.SetActive(false);
            RectTransform phase2Rect = phase2Root.GetComponent<RectTransform>();
            ConfigureRootRect(phase2Rect);
            phase2Rect.SetSiblingIndex(2);

            GameObject baseLayer = FindOrCreate(phase2Root, "PaintLayer");
            ConfigureStretch(baseLayer.GetComponent<RectTransform>());
            baseLayer.transform.SetSiblingIndex(0);
            Image baseImage = GetOrAdd<Image>(baseLayer);
            Undo.RecordObject(baseImage, "Configure Phase 2 base layer");
            Sprite paint=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Ingame/Floor/Paint.png");
            Sprite dust=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Ingame/Floor/Dust.png");
            if(!paint||!dust)throw new InvalidOperationException("Required Phase 2 Dust/Paint Sprite assets are missing or not imported as Sprite.");
            baseImage.sprite = paint;
            baseImage.color = Color.white;
            baseImage.raycastTarget = false;

            GameObject coverLayer = FindOrCreate(phase2Root, "DustLayer");
            ConfigureStretch(coverLayer.GetComponent<RectTransform>());
            coverLayer.transform.SetSiblingIndex(1);
            RawImage cover = GetOrAdd<RawImage>(coverLayer);
            Undo.RecordObject(cover, "Configure Phase 2 cover layer");
            cover.color = Color.white;
            cover.texture = null;
            cover.material = material;
            cover.raycastTarget = false;
            Phase2MaskPresenter presenter = GetOrAdd<Phase2MaskPresenter>(coverLayer);

            GameObject inputLayer = FindOrCreate(phase2Root, "Phase2InputSurface");
            ConfigureStretch(inputLayer.GetComponent<RectTransform>());
            inputLayer.transform.SetSiblingIndex(2);
            Image inputImage = GetOrAdd<Image>(inputLayer);
            Undo.RecordObject(inputImage, "Configure Phase 2 input surface");
            inputImage.sprite = null;
            inputImage.color = new Color(1f, 1f, 1f, 0f);
            inputImage.raycastTarget = true;
            Phase2PointerInputController pointer = GetOrAdd<Phase2PointerInputController>(inputLayer);

            Phase2PhaseAdapter adapter = GetOrAdd<Phase2PhaseAdapter>(phase2Root);
            GameScoreController score = general.GetComponent<GameScoreController>();
            GameSessionController session = general.GetComponent<GameSessionController>();
            Phase1PhaseAdapter phase1Adapter = phase1Root.GetComponent<Phase1PhaseAdapter>();
            Phase1BoardController phase1Board = phase1Root.GetComponent<Phase1BoardController>();
            if (!score || !session || !phase1Adapter || !phase1Board)
                throw new InvalidOperationException("Required Stage 5A GameFlow references are missing.");

            SetReferences(presenter, ("paintedLayer", baseImage), ("cover", cover), ("materialTemplate", material), ("dustSprite", dust), ("paintSprite", paint));
            SetReferences(pointer, ("adapter", adapter), ("inputSurface", inputLayer.GetComponent<RectTransform>()));
            SetReferences(adapter, ("maskPresenter", presenter), ("pointerInput", pointer), ("scoreController", score));
            SetObjectArray(session, "phases", new UnityEngine.Object[] { phase1Adapter, adapter });
            SetInt(session, "initialPhase", (int)GamePhaseId.Phase1);
            SetInt(session, "difficulty", (int)GameDifficulty.Hard);
            SetBool(phase1Board, "generateOnStart", false);

            phase2Root.SetActive(false);
            EditorUtility.SetDirty(phase2Root);
            EditorUtility.SetDirty(session);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("INGAME scene save failed.");
            Debug.Log("[Phase2Stage5B] Scene integration setup completed.");
        }

        private static GameObject Require(string path)
        {
            GameObject value = GameObject.Find(path);
            if (!value) throw new InvalidOperationException("Required object missing: " + path);
            return value;
        }

        private static GameObject FindOrCreate(GameObject parent, string name)
        {
            Transform existing = parent.transform.Find(name);
            if (existing) return existing.gameObject;
            var value = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(value, "Create " + name);
            value.transform.SetParent(parent.transform, false);
            return value;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            T existing = target.GetComponent<T>();
            return existing ? existing : Undo.AddComponent<T>(target);
        }

        private static void ConfigureRootRect(RectTransform rect)
        {
            Undo.RecordObject(rect, "Configure Phase2Root RectTransform");
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-7f, -1f);
            rect.sizeDelta = new Vector2(1250f, 1250f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void ConfigureStretch(RectTransform rect)
        {
            Undo.RecordObject(rect, "Configure Phase 2 layer RectTransform");
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetReferences(Component target, params (string name, UnityEngine.Object value)[] values)
        {
            Undo.RecordObject(target, "Wire " + target.GetType().Name);
            var serialized = new SerializedObject(target);
            foreach ((string name, UnityEngine.Object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + name + " missing.");
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedProperties();
        }

        private static void SetObjectArray(Component target, string name, UnityEngine.Object[] values)
        {
            Undo.RecordObject(target, "Configure " + name);
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + name + " missing.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedProperties();
        }

        private static void SetInt(Component target, string name, int value)
        {
            Undo.RecordObject(target, "Configure " + name);
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + name + " missing.");
            property.intValue = value;
            serialized.ApplyModifiedProperties();
        }

        private static void SetBool(Component target, string name, bool value)
        {
            Undo.RecordObject(target, "Configure " + name);
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + name + " missing.");
            property.boolValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
#endif
