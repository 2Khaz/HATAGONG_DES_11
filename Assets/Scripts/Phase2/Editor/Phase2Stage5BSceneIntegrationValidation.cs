#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using HATAGONG.GameFlow;
using HATAGONG.Phase1;
using HATAGONG.Phase2.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.Phase2.Editor
{
    public static class Phase2Stage5BSceneIntegrationValidation
    {
        private const string ScenePath = "Assets/Scenes/INGAME.unity";
        private const string MaterialPath = "Assets/Materials/Phase2/Phase2BlackCoverMask.mat";

        [MenuItem("Tools/HATAGONG/Phase2/Validate Stage 5B Scene Integration")]
        public static void Validate()
        {
            Debug.Log("[Phase2Stage5B][Test] validation started");
            int passed = 0;
            int total = 0;
            void Check(bool condition, string name)
            {
                total++;
                if (!condition) throw new InvalidOperationException("[Phase2Stage5B][Test] " + name);
                passed++;
            }

            ValidateScene(Check);
            ValidateDifficulty(Check);
            ValidatePointerMapping(Check);
            ValidateLifecycleAndGameplay(Check);
            Debug.Log($"[Phase2Stage5B][Test] result={passed}/{total}, failures=0");
        }

        private static void ValidateScene(Action<bool, string> check)
        {
            Scene scene = SceneManager.GetActiveScene();
            check(scene.path == ScenePath, "INGAME scene is active");
            GameObject middle = GameObject.Find("Canvas/Game_UI_General/Middle_GamePanel");
            check(middle, "Middle_GamePanel exists");
            Transform phase1Transform = middle.transform.Find("Phase1_FieldRoot");
            check(phase1Transform, "Phase1_FieldRoot exists");

            int phase2Count = 0;
            Transform phase2Transform = null;
            for (int i = 0; i < middle.transform.childCount; i++)
            {
                Transform child = middle.transform.GetChild(i);
                if (child.name == "Phase2Root") { phase2Count++; phase2Transform = child; }
            }
            check(phase2Count == 1, "Phase2Root exists exactly once");
            GameObject phase2Root = phase2Transform.gameObject;
            check(phase2Transform.GetSiblingIndex() == 2, "Phase2Root sibling index is 2");
            check(!phase2Root.activeSelf, "Phase2Root is initially inactive");

            RectTransform phase1Rect = phase1Transform as RectTransform;
            RectTransform phase2Rect = phase2Transform as RectTransform;
            check(phase2Rect && phase2Rect.anchorMin == new Vector2(0.5f, 0.5f) && phase2Rect.anchorMax == new Vector2(0.5f, 0.5f) && phase2Rect.pivot == new Vector2(0.5f, 0.5f), "Phase2Root anchors and pivot exact");
            check(phase2Rect.anchoredPosition == new Vector2(-7f, -1f) && phase2Rect.sizeDelta == new Vector2(1250f, 1250f), "Phase2Root position and size exact");
            check(phase2Rect.localScale == Vector3.one && phase2Rect.localRotation == Quaternion.identity, "Phase2Root transform identity");
            check(RectEquals(phase1Rect, phase2Rect), "Phase1 and Phase2 roots have equal RectTransform layout");
            check(phase2Transform.childCount == 3, "Phase2Root has exactly three production layers");

            Transform baseLayer = phase2Transform.Find("CementBaseLayer_Gray");
            Transform coverLayer = phase2Transform.Find("BlackCoverLayer");
            Transform inputLayer = phase2Transform.Find("Phase2InputSurface");
            check(baseLayer && coverLayer && inputLayer, "all three named Phase2 layers exist");
            check(baseLayer.GetSiblingIndex() == 0 && coverLayer.GetSiblingIndex() == 1 && inputLayer.GetSiblingIndex() == 2, "Phase2 layer sibling order exact");
            check(IsFullStretch(baseLayer as RectTransform) && IsFullStretch(coverLayer as RectTransform) && IsFullStretch(inputLayer as RectTransform), "all Phase2 layers full stretch");

            Image baseImage = baseLayer.GetComponent<Image>();
            check(baseImage && Approximately(baseImage.color.a, 1f) && !baseImage.raycastTarget, "gray base image is opaque and non-raycast");
            RawImage cover = coverLayer.GetComponent<RawImage>();
            check(cover && Approximately(cover.color.r, 0f) && Approximately(cover.color.g, 0f) && Approximately(cover.color.b, 0f) && Approximately(cover.color.a, 1f) && !cover.raycastTarget, "black cover is opaque and non-raycast");
            check(cover.material && cover.material.shader && cover.material.shader.name == Phase2MaskPresenter.ShaderName && Approximately(cover.material.GetFloat("_MaskBound"), 0f), "cover material guarantees black before mask binding");
            Image inputImage = inputLayer.GetComponent<Image>();
            check(inputImage && Approximately(inputImage.color.a, 0f) && inputImage.raycastTarget, "input surface is transparent and raycastable");
            check(coverLayer.GetComponent<Phase2MaskPresenter>() && inputLayer.GetComponent<Phase2PointerInputController>(), "presenter and pointer components exist");

            Phase2PhaseAdapter phase2Adapter = phase2Root.GetComponent<Phase2PhaseAdapter>();
            check(phase2Adapter && phase2Adapter.PhaseId == GamePhaseId.Phase2, "Phase2 adapter exists with Phase2 id");
            GameObject general = GameObject.Find("Canvas/Game_UI_General");
            GameSessionController session = general ? general.GetComponent<GameSessionController>() : null;
            check(session, "GameSessionController exists");
            SerializedObject sessionData = new SerializedObject(session);
            SerializedProperty phases = sessionData.FindProperty("phases");
            check(phases != null && phases.arraySize == 2, "session has exactly two registered phases");
            check(phases.GetArrayElementAtIndex(0).objectReferenceValue is Phase1PhaseAdapter && ReferenceEquals(phases.GetArrayElementAtIndex(1).objectReferenceValue, phase2Adapter), "session phase order is Phase1 then Phase2");
            check((GamePhaseId)sessionData.FindProperty("initialPhase").intValue == GamePhaseId.Phase1, "initial phase remains Phase1");
            check((GameDifficulty)sessionData.FindProperty("difficulty").intValue == GameDifficulty.Hard, "scene difficulty remains Hard");
            var boardData = new SerializedObject(phase1Transform.GetComponent<Phase1BoardController>());
            check(!boardData.FindProperty("generateOnStart").boolValue, "Phase1 generateOnStart remains false");

            PhaseTransitionController transition = general.GetComponent<PhaseTransitionController>();
            SerializedObject transitionData = new SerializedObject(transition);
            var overlay = transitionData.FindProperty("overlay").objectReferenceValue as PhaseTransitionOverlay;
            check(overlay && Approximately(overlay.TotalConfiguredDuration, 0.75f), "transition overlay duration remains 0.75 seconds");
        }

        private static void ValidateDifficulty(Action<bool, string> check)
        {
            check((int)GameDifficulty.Unspecified == 0 && (int)GameDifficulty.Easy == 1 && (int)GameDifficulty.Normal == 2 && (int)GameDifficulty.Hard == 3, "GameDifficulty numeric contract preserved");
            check(Phase2PhaseAdapter.TryGetPreset(GameDifficulty.Easy, out Phase2PaintConfig easy, out float easyRadius) && Approximately(easyRadius, 0.085f) && Approximately((float)easy.EasyRadiusRatio, 0.085f), "Easy maps to 0.085 preset");
            check(Phase2PhaseAdapter.TryGetPreset(GameDifficulty.Normal, out Phase2PaintConfig normal, out float normalRadius) && Approximately(normalRadius, 0.075f) && Approximately((float)normal.NormalRadiusRatio, 0.075f), "Normal maps to 0.075 preset");
            check(Phase2PhaseAdapter.TryGetPreset(GameDifficulty.Hard, out Phase2PaintConfig hard, out float hardRadius) && Approximately(hardRadius, 0.065f) && Approximately((float)hard.HardRadiusRatio, 0.065f), "Hard maps to 0.065 preset");
            check(!Phase2PhaseAdapter.TryGetPreset(GameDifficulty.Unspecified, out _, out _), "Unspecified mapping is rejected");
            check(hard.Width == 128 && hard.Height == 128 && hard.TotalCellCount == 16384 && hard.RequiredClearCells == 16221 && Math.Abs(hard.ClearRatio - 0.99d) < 0.0000001d, "production grid and clear threshold preserved");
            check(hard.CoverageScoreBudget == 500 && hard.QuarterMilestoneBonus == 100 && hard.HalfMilestoneBonus == 150 && hard.ThreeQuarterMilestoneBonus == 200 && hard.ClearBonus == 500, "production score constants preserved");
            check(Math.Abs(hard.StampSpacingRatio - 0.4d) < 0.0000001d && Phase2PaintOrchestrator.MaximumBatchStampCount == 65536 && Phase2PaintOrchestrator.VisualReplayChunkSize == 4096, "spacing and batch constants preserved");
        }

        private static void ValidatePointerMapping(Action<bool, string> check)
        {
            var rect = new Rect(-625f, -625f, 1250f, 1250f);
            check(Phase2PointerInputController.NormalizeLocalPoint(rect, Vector2.zero) == new Vector2(0.5f, 0.5f), "pointer center maps to board center");
            check(Phase2PointerInputController.NormalizeLocalPoint(rect, new Vector2(-625f, -625f)) == Vector2.zero, "pointer lower-left maps to zero");
            check(Phase2PointerInputController.NormalizeLocalPoint(rect, new Vector2(625f, 625f)) == Vector2.one, "pointer upper-right maps to one");
            Vector2 outside = Phase2PointerInputController.NormalizeLocalPoint(rect, new Vector2(-750f, 700f));
            check(outside.x < 0f && outside.y > 1f, "pointer mapping does not square-clamp outside coordinates");
        }

        private static void ValidateLifecycleAndGameplay(Action<bool, string> check)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            check(material && material.shader && material.shader.name == Phase2MaskPresenter.ShaderName, "Phase2 material and shader assets load");
            check(Approximately(Phase2MaskPresenter.CoverAlphaFromMask(0f), 1f) && Approximately(Phase2MaskPresenter.CoverAlphaFromMask(0.5f), 0.5f) && Approximately(Phase2MaskPresenter.CoverAlphaFromMask(1f), 0f), "mask inversion maps 0 0.5 1 exactly");
            check(Approximately(Phase2MaskPresenter.CoverAlphaFromMask(0f, 0f), 1f) && Approximately(Phase2MaskPresenter.CoverAlphaFromMask(0f, 1f), 0f), "completion fill preserves the initial cover and can fully clear it");
            check(Phase2PhaseAdapter.CompletionDurationSeconds >= 0.3f && Phase2PhaseAdapter.CompletionDurationSeconds <= 0.5f, "visual completion duration stays within the 0.3 to 0.5 second contract");
            ValidateUnboundMaskGpu(check, material);
            ValidatePresenterDestroyOwnership(check, material);
            ValidatePresenterOnDestroyOwnership(check, material);

            GameObject root = new GameObject("Phase2Stage5B_FixtureRoot", typeof(RectTransform));
            root.SetActive(false);
            RenderTexture firstMask = null;
            Texture2D readback = null;
            try
            {
                GameScoreController score = root.AddComponent<GameScoreController>();
                Phase2PhaseAdapter adapter = root.AddComponent<Phase2PhaseAdapter>();
                GameObject coverObject = CreateChild(root, "BlackCover", typeof(RawImage));
                RawImage cover = coverObject.GetComponent<RawImage>();
                cover.color = Color.black;
                cover.material = material;
                Phase2MaskPresenter presenter = coverObject.AddComponent<Phase2MaskPresenter>();
                GameObject inputObject = CreateChild(root, "Input", typeof(Image));
                Image inputImage = inputObject.GetComponent<Image>();
                inputImage.color = new Color(1f, 1f, 1f, 0f);
                inputImage.raycastTarget = true;
                Phase2PointerInputController pointer = inputObject.AddComponent<Phase2PointerInputController>();
                SetReferences(presenter, ("cover", cover), ("materialTemplate", material));
                SetReferences(pointer, ("adapter", adapter), ("inputSurface", inputObject.GetComponent<RectTransform>()));
                SetReferences(adapter, ("maskPresenter", presenter), ("pointerInput", pointer), ("scoreController", score));

                check(adapter.Prepare(new GameRunContext(GameDifficulty.Hard)), "inactive Phase2 root prepares successfully");
                check(adapter.IsPrepared && !adapter.IsRunning && !adapter.InputEnabled && adapter.CurrentState == Phase2PaintSessionState.Ready, "prepare state is ready with input off");
                check(adapter.RendererInitialized && adapter.OwnedRenderTextureCount == 3 && adapter.MaskTexture && adapter.MaskTexture.width == 1024 && adapter.MaskTexture.height == 1024, "prepare owns three 1024 render textures");
                check(adapter.MaskFormat == Phase2PaintMaskRenderer.SelectMaskFormat(), "mask format follows R8 fallback contract");
                firstMask = adapter.MaskTexture;
                readback = ReadMask(firstMask, readback);
                check(MaximumRed(readback) == 0, "prepared mask is cleared to zero");
                check(presenter.IsBound && ReferenceEquals(presenter.BoundTexture, firstMask) && presenter.HasRuntimeMaterial, "presenter binds runtime mask with one material instance");
                check(Approximately(Phase2MaskPresenter.CoverAlphaFromMask(MaximumRed(readback) / 255f), 1f), "initial zero mask produces opaque black cover");
                Texture sharedTextureBefore = material.GetTexture("_MainTex");
                float sharedMaskBoundBefore = material.GetFloat("_MaskBound");
                float sharedCompletionFillBefore = material.GetFloat("_CompletionFill");
                Material firstRuntimeMaterial = GetRuntimeMaterial(presenter);
                check(firstRuntimeMaterial && !ReferenceEquals(firstRuntimeMaterial, material) && ReferenceEquals(cover.material, firstRuntimeMaterial) && Approximately(firstRuntimeMaterial.GetFloat("_CompletionFill"), 0f), "bind assigns a distinct runtime material with zero completion fill to the graphic");
                check(ReferenceEquals(material.GetTexture("_MainTex"), sharedTextureBefore) && Approximately(material.GetFloat("_MaskBound"), sharedMaskBoundBefore) && Approximately(material.GetFloat("_CompletionFill"), sharedCompletionFillBefore), "bind leaves the shared material asset unchanged");
                check(presenter.Bind(adapter.MaskTexture), "presenter rebind succeeds");
                Material reboundRuntimeMaterial = GetRuntimeMaterial(presenter);
                check(reboundRuntimeMaterial && !ReferenceEquals(reboundRuntimeMaterial, firstRuntimeMaterial) && !firstRuntimeMaterial && ReferenceEquals(cover.material, reboundRuntimeMaterial), "rebind replaces and destroys the previous runtime material");
                check(ReferenceEquals(material.GetTexture("_MainTex"), sharedTextureBefore) && Approximately(material.GetFloat("_MaskBound"), sharedMaskBoundBefore) && Approximately(material.GetFloat("_CompletionFill"), sharedCompletionFillBefore), "rebind leaves the shared material asset unchanged");
                presenter.ReleaseBinding();
                check(!presenter.HasRuntimeMaterial && !GetRuntimeMaterial(presenter) && !cover.texture && ReferenceEquals(cover.material, material), "release binding clears runtime material and texture references");
                check(firstMask.IsCreated() && ReferenceEquals(material.GetTexture("_MainTex"), sharedTextureBefore) && Approximately(material.GetFloat("_MaskBound"), sharedMaskBoundBefore) && Approximately(material.GetFloat("_CompletionFill"), sharedCompletionFillBefore), "release binding preserves mask ownership and shared material state");

                check(adapter.Activate() && root.activeSelf && adapter.IsRunning, "activate enables root and starts core");
                check(!adapter.InputEnabled && !pointer.InputEnabled, "activate keeps input off before overlay completion");
                adapter.SetInputEnabled(true);
                check(adapter.InputEnabled && pointer.InputEnabled, "explicit input enable opens pointer gate");

                int scoreBeforeStamp = score.CurrentScore;
                check(adapter.TryBeginStroke(new Vector2(0.5f, 0.5f), out Phase2OrchestrationResult firstStamp), "first pointer stamp is submitted");
                check(firstStamp.InputStampCount == 1 && firstStamp.LogicAcceptedCount == 1 && firstStamp.VisualSubmittedCount == 1 && firstStamp.HistoryAddedCount == 1, "first stamp has no logic or visual omission");
                check(adapter.PaintedCellCount > 0 && adapter.Progress01 > 0d, "actual stroke increases coverage");
                check(score.CurrentScore == scoreBeforeStamp + firstStamp.ScoreDelta && adapter.PhaseScore == firstStamp.ScoreDelta, "first score delta is applied exactly once");
                readback = ReadMask(adapter.MaskTexture, readback);
                check(SampleRed(readback, 0.5f, 0.5f) > 0.98f, "visual mask paints in the same progress direction");

                int generation = adapter.RuntimeGenerationCount;
                PointerEventData heldPointer = CreatePointerEvent(7, Vector2.zero);
                pointer.OnPointerDown(heldPointer);
                check(pointer.HasActivePointer && pointer.ActivePointerId == 7, "first active pointer id is retained");
                pointer.OnPointerDown(CreatePointerEvent(8, Vector2.zero));
                check(pointer.ActivePointerId == 7, "second pointer is ignored while a stroke is active");
                pointer.OnPointerExit(heldPointer);
                check(pointer.HasActivePointer && pointer.ActivePointerId == 7, "pointer exit preserves the active stroke without press-state inference");
                int paintedBeforeExitDrag = adapter.PaintedCellCount;
                heldPointer.position = new Vector2(35f, 0f);
                pointer.OnDrag(heldPointer);
                check(pointer.HasActivePointer && adapter.PaintedCellCount > paintedBeforeExitDrag, "drag continues after pointer exit with the same pointer");
                pointer.OnPointerUp(heldPointer);
                check(!pointer.HasActivePointer, "pointer up ends the active stroke");
                pointer.OnEndDrag(heldPointer);
                check(!pointer.HasActivePointer, "end drag after pointer up is idempotent");
                int paintedAfterPointerUp = adapter.PaintedCellCount;
                heldPointer.position = new Vector2(-35f, 0f);
                pointer.OnDrag(heldPointer);
                check(adapter.PaintedCellCount == paintedAfterPointerUp, "drag after pointer up is ignored");

                PointerEventData exitIndependentPointer = CreatePointerEvent(9, Vector2.zero);
                pointer.OnPointerDown(exitIndependentPointer);
                check(pointer.HasActivePointer && pointer.ActivePointerId == 9, "exit-state-independent fixture begins an active stroke");
                pointer.OnPointerExit(CreatePointerEvent(9, Vector2.zero));
                check(pointer.HasActivePointer && pointer.ActivePointerId == 9, "pointer exit never cancels the matching active stroke");
                exitIndependentPointer.position = new Vector2(75f, 0f);
                pointer.OnDrag(exitIndependentPointer);
                exitIndependentPointer.position = new Vector2(90f, 0f);
                pointer.OnDrag(exitIndependentPointer);
                check(pointer.HasActivePointer && pointer.ActivePointerId == 9, "geometry-rejected outside drag preserves the active stroke");
                int paintedBeforeReentryDrag = adapter.PaintedCellCount;
                exitIndependentPointer.position = new Vector2(0f, 35f);
                pointer.OnDrag(exitIndependentPointer);
                check(pointer.HasActivePointer && adapter.PaintedCellCount > paintedBeforeReentryDrag, "drag after pointer reentry continues coverage");
                pointer.OnEndDrag(exitIndependentPointer);
                check(!pointer.HasActivePointer, "end drag ends the matching active stroke");
                int paintedAfterEndDrag = adapter.PaintedCellCount;
                pointer.OnEndDrag(exitIndependentPointer);
                pointer.OnDrag(exitIndependentPointer);
                check(!pointer.HasActivePointer && adapter.PaintedCellCount == paintedAfterEndDrag, "duplicate end drag and later drag are ignored");

                PointerEventData focusPointer = CreatePointerEvent(10, Vector2.zero);
                pointer.OnPointerDown(focusPointer);
                pointer.OnApplicationFocus(false);
                check(!pointer.HasActivePointer, "application focus loss cancels the active stroke");
                PointerEventData pausePointer = CreatePointerEvent(11, Vector2.zero);
                pointer.OnPointerDown(pausePointer);
                pointer.OnApplicationPause(true);
                check(!pointer.HasActivePointer, "application pause cancels the active stroke");
                PointerEventData disabledPointer = CreatePointerEvent(12, Vector2.zero);
                pointer.OnPointerDown(disabledPointer);
                pointer.SetInputEnabled(false);
                check(!pointer.HasActivePointer && !pointer.InputEnabled, "input disable cancels the active stroke");
                pointer.SetInputEnabled(true);
                int paintedBeforeDeactivate = adapter.PaintedCellCount;
                int phaseScoreBeforeDeactivate = adapter.PhaseScore;
                Phase2PaintOrchestrator orchestratorBeforeDeactivate = GetOwnedOrchestrator(adapter);
                RenderTexture maskBeforeDeactivate = adapter.MaskTexture;
                Material presenterMaterialBeforeDeactivate = GetRuntimeMaterial(presenter);
                check(orchestratorBeforeDeactivate != null && orchestratorBeforeDeactivate.IsPrepared, "deactivate fixture captures a live prepared orchestrator");
                check(maskBeforeDeactivate && maskBeforeDeactivate.IsCreated(), "deactivate fixture captures the current created mask texture");
                adapter.Deactivate();
                check(!root.activeSelf && !adapter.IsRunning && !adapter.InputEnabled && !pointer.HasActivePointer, "deactivate hides root and cancels pointer input");
                check(adapter.IsPrepared && adapter.PaintedCellCount == paintedBeforeDeactivate, "deactivate preserves prepared painted progress");
                check(adapter.PhaseScore == phaseScoreBeforeDeactivate, "deactivate preserves the phase score");
                check(ReferenceEquals(GetOwnedOrchestrator(adapter), orchestratorBeforeDeactivate), "deactivate preserves the orchestrator reference");
                check(orchestratorBeforeDeactivate.IsPrepared, "deactivate keeps the orchestrator prepared");
                check(ReferenceEquals(adapter.MaskTexture, maskBeforeDeactivate), "deactivate preserves the current mask reference");
                check(maskBeforeDeactivate.IsCreated() && adapter.MaskTexture.IsCreated(), "deactivate keeps the owned mask GPU allocation created");
                check(!presenter.HasRuntimeMaterial && !presenterMaterialBeforeDeactivate && !cover.texture && ReferenceEquals(cover.material, material), "deactivate releases only the presentation material and binding");

                bool reactivated = adapter.Activate();
                check(reactivated, "reactivate succeeds with the preserved prepared runtime: " + adapter.LastFailureReason);
                check(ReferenceEquals(GetOwnedOrchestrator(adapter), orchestratorBeforeDeactivate), "reactivate reuses the existing orchestrator reference");
                check(adapter.RuntimeGenerationCount == generation, "reactivate does not create a new runtime generation");
                check(ReferenceEquals(adapter.MaskTexture, maskBeforeDeactivate), "reactivate reuses the current mask reference");
                check(maskBeforeDeactivate.IsCreated() && adapter.MaskTexture.IsCreated(), "reactivate keeps the reused mask GPU allocation created");
                Material presenterMaterialAfterReactivate = GetRuntimeMaterial(presenter);
                check(presenter.IsBound && ReferenceEquals(presenter.BoundTexture, maskBeforeDeactivate) && presenterMaterialAfterReactivate && !ReferenceEquals(presenterMaterialAfterReactivate, material), "reactivate binds a valid new presentation material to the preserved mask");
                check(!adapter.InputEnabled && !adapter.TryBeginStroke(new Vector2(0.85f, 0.85f), out _) && adapter.PaintedCellCount == paintedBeforeDeactivate && adapter.PhaseScore == phaseScoreBeforeDeactivate, "reactivated phase rejects input before explicit enable");
                adapter.SetInputEnabled(true);
                check(adapter.InputEnabled, "reactivated phase accepts input only after explicit enable");
                int paintedBeforeReactivatedStamp = adapter.PaintedCellCount;
                int phaseScoreBeforeReactivatedStamp = adapter.PhaseScore;
                int totalScoreBeforeReactivatedStamp = score.CurrentScore;
                check(adapter.TryBeginStroke(new Vector2(0.85f, 0.85f), out Phase2OrchestrationResult reactivatedStamp), "reactivated runtime accepts a new production request");
                check(adapter.PaintedCellCount > paintedBeforeReactivatedStamp && reactivatedStamp.PaintedCellDelta == adapter.PaintedCellCount - paintedBeforeReactivatedStamp, "reactivated runtime increases coverage");
                check(reactivatedStamp.ScoreDelta > 0 && adapter.PhaseScore == phaseScoreBeforeReactivatedStamp + reactivatedStamp.ScoreDelta && score.CurrentScore == totalScoreBeforeReactivatedStamp + reactivatedStamp.ScoreDelta, "reactivated runtime applies a new score delta exactly once");

                RenderTexture oldMask = adapter.MaskTexture;
                check(adapter.Prepare(new GameRunContext(GameDifficulty.Hard)), "repeated prepare creates a clean new run");
                check((!oldMask || !oldMask.IsCreated()) && adapter.MaskTexture && !ReferenceEquals(oldMask, adapter.MaskTexture) && adapter.RuntimeGenerationCount == generation + 1, "reprepare releases old GPU resources exactly once");
                check(adapter.PaintedCellCount == 0 && adapter.PhaseScore == 0 && !adapter.IsCleared && !adapter.IsExitReady, "reprepare resets run state");
                check(!adapter.Prepare(new GameRunContext(GameDifficulty.Unspecified)) && !adapter.IsPrepared && !adapter.IsRunning && !root.activeSelf && adapter.OwnedRenderTextureCount == 0, "Unspecified prepare fails closed and releases partial resources");

                score.ResetForNewSession();
                check(adapter.Prepare(new GameRunContext(GameDifficulty.Hard)) && adapter.Activate(), "gameplay fixture re-prepares and activates");
                adapter.SetInputEnabled(true);
                int clearedCount = 0;
                int exitReadyCount = 0;
                int scoreAtCleared = -1;
                int scoreAtExitReady = -1;
                adapter.PhaseCleared += () => { clearedCount++; scoreAtCleared = score.CurrentScore; };
                adapter.PhaseExitReady += () => { exitReadyCount++; scoreAtExitReady = score.CurrentScore; };

                var belowThresholdStamps = new List<Phase2PaintStamp>(adapter.RequiredClearCells - 1);
                for (int i = 0; i < adapter.RequiredClearCells - 1; i++)
                {
                    int x = i % 128;
                    int y = i / 128;
                    belowThresholdStamps.Add(new Phase2PaintStamp((x + 0.5f) / 128f, (y + 0.5f) / 128f, 0.0001f));
                }
                check(adapter.TryApplyStampBatch(belowThresholdStamps, out Phase2OrchestrationResult belowThreshold), "16220-cell integration batch submits");
                check(belowThreshold.LogicAcceptedCount == 16220 && belowThreshold.VisualSubmittedCount == 16220 && adapter.PaintedCellCount == 16220, "16220-cell batch has no omission");
                check(!belowThreshold.ClearThresholdReached && !adapter.IsCleared && clearedCount == 0 && exitReadyCount == 0, "phase remains uncleared below 99 percent threshold");
                check(belowThreshold.ReachedMilestones == (Phase2MilestoneFlags.Quarter | Phase2MilestoneFlags.Half | Phase2MilestoneFlags.ThreeQuarter), "all milestones cross once before threshold");

                int scoreBeforeThresholdReactivate = score.CurrentScore;
                adapter.Deactivate();
                check(adapter.IsPrepared && adapter.PaintedCellCount == 16220 && score.CurrentScore == scoreBeforeThresholdReactivate, "threshold fixture preserves state through deactivate");
                check(adapter.Activate() && !adapter.InputEnabled && adapter.PaintedCellCount == 16220, "threshold fixture reactivates with input off");

                int thresholdIndex = adapter.RequiredClearCells - 1;
                var finalStamp = new List<Phase2PaintStamp>
                {
                    new Phase2PaintStamp(((thresholdIndex % 128) + 0.5f) / 128f, ((thresholdIndex / 128) + 0.5f) / 128f, 0.0001f)
                };
                int scoreBeforeFinal = score.CurrentScore;
                check(!adapter.TryApplyStampBatch(finalStamp, out _) && adapter.PaintedCellCount == 16220 && score.CurrentScore == scoreBeforeFinal, "reactivated threshold fixture rejects final input before explicit enable");
                adapter.SetInputEnabled(true);
                check(adapter.TryApplyStampBatch(finalStamp, out Phase2OrchestrationResult clearResult), "16221st cell reaches clear threshold");
                check(clearResult.ClearThresholdReached && adapter.PaintedCellCount == 16221 && adapter.IsCleared && !adapter.IsExitReady, "clear state is exact at 16221 cells while exit ready waits");
                check(clearedCount == 1 && exitReadyCount == 0 && scoreAtCleared == scoreBeforeFinal, "PhaseCleared fires once before final score delivery and visual completion");
                check(score.CurrentScore == scoreBeforeFinal + clearResult.ScoreDelta && scoreAtExitReady < 0, "final Phase2 score is applied before delayed PhaseExitReady");
                check(adapter.PhaseScore == 1450 && score.CurrentScore == 1450, "Phase2 maximum score is 1450");
                check(!adapter.InputEnabled && !pointer.InputEnabled, "clear immediately locks Phase2 input");
                check(presenter.IsCompleting && Approximately(presenter.CompletionFill, 0f), "visual completion starts from zero fill");
                int finalScore = score.CurrentScore;
                int finalPainted = adapter.PaintedCellCount;
                AdvancePresenterCompletion(presenter, Phase2PhaseAdapter.CompletionDurationSeconds * 0.5f);
                check(presenter.IsCompleting && presenter.CompletionFill > 0f && presenter.CompletionFill < 1f && exitReadyCount == 0, "visual completion exposes an intermediate fill before exit ready");
                check(adapter.PaintedCellCount == finalPainted && adapter.PhaseScore == 1450 && score.CurrentScore == finalScore, "visual completion midpoint preserves logic and score");
                AdvancePresenterCompletion(presenter, Phase2PhaseAdapter.CompletionDurationSeconds * 0.51f);
                check(!presenter.IsCompleting && Approximately(presenter.CompletionFill, 1f), "visual completion reaches full fill");
                check(Approximately(Phase2MaskPresenter.CoverAlphaFromMask(0f, presenter.CompletionFill), 0f), "full completion fill makes the cover fully transparent");
                check(adapter.IsExitReady && exitReadyCount == 1 && scoreAtExitReady == score.CurrentScore, "PhaseExitReady fires once after final score and visual completion");
                check(adapter.PaintedCellCount == finalPainted && adapter.PhaseScore == 1450 && score.CurrentScore == finalScore, "visual completion finish adds no logic or score mutation");
                AdvancePresenterCompletion(presenter, Phase2PhaseAdapter.CompletionDurationSeconds);
                check(exitReadyCount == 1 && score.CurrentScore == finalScore && adapter.PaintedCellCount == finalPainted, "duplicate visual completion advance cannot duplicate exit score or logic");
                check(!adapter.TryApplyStampBatch(finalStamp, out _) && score.CurrentScore == finalScore && adapter.PaintedCellCount == finalPainted && clearedCount == 1 && exitReadyCount == 1, "post-clear duplicate input cannot duplicate score or events");
                check(adapter.Prepare(new GameRunContext(GameDifficulty.Hard)) && Approximately(presenter.CompletionFill, 0f) && !presenter.IsCompleting && !adapter.IsCleared && !adapter.IsExitReady, "prepare resets visual completion fill and exit state");
                check(score.CurrentScore == finalScore && adapter.PhaseScore == 0 && adapter.PaintedCellCount == 0, "prepare resets only Phase2 runtime state without changing total score");

                Phase2PaintOrchestrator destroyOrchestrator = GetOwnedOrchestrator(adapter);
                RenderTexture destroyMask = adapter.MaskTexture;
                check(destroyOrchestrator != null && destroyOrchestrator.IsPrepared && destroyMask && destroyMask.IsCreated(), "OnDestroy fixture has a live owned orchestrator and mask");
                MethodInfo adapterOnDestroy = typeof(Phase2PhaseAdapter).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
                check(adapterOnDestroy != null, "adapter OnDestroy lifecycle method is discoverable");
                adapterOnDestroy.Invoke(adapter, null);
                check(GetOwnedOrchestrator(adapter) == null, "OnDestroy clears the owned orchestrator reference");
                check(!destroyOrchestrator.IsPrepared && destroyOrchestrator.OwnedRenderTextureCount == 0 && destroyOrchestrator.VisualHistoryCount == 0, "OnDestroy disposes the owned orchestrator");
                check(!destroyMask || !destroyMask.IsCreated(), "OnDestroy releases owned GPU resources");
                check(!presenter.HasRuntimeMaterial && !cover.texture && ReferenceEquals(cover.material, material), "adapter OnDestroy leaves the presenter binding safe");
                adapterOnDestroy.Invoke(adapter, null);
                check(GetOwnedOrchestrator(adapter) == null, "repeated adapter OnDestroy is idempotent");
                GameObject destroyedRoot = root;
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                check(!destroyedRoot, "destroying the cleaned adapter host is idempotent");
            }
            finally
            {
                if (readback) UnityEngine.Object.DestroyImmediate(readback);
                if (root) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateUnboundMaskGpu(Action<bool, string> check, Material sharedMaterial)
        {
            Material runtimeMaterial = null;
            RenderTexture target = null;
            Texture2D readback = null;
            Texture sharedTextureBefore = sharedMaterial.GetTexture("_MainTex");
            float sharedMaskBoundBefore = sharedMaterial.GetFloat("_MaskBound");
            float sharedCompletionFillBefore = sharedMaterial.GetFloat("_CompletionFill");
            RenderTexture previous = RenderTexture.active;
            try
            {
                runtimeMaterial = new Material(sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
                runtimeMaterial.SetTexture("_MainTex", null);
                runtimeMaterial.SetFloat("_MaskBound", 0f);
                runtimeMaterial.SetFloat("_CompletionFill", 0f);
                target = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!target.Create()) throw new InvalidOperationException("[Phase2Stage5B][Test] unbound mask GPU target creation failed");
                Graphics.Blit(Texture2D.whiteTexture, target, runtimeMaterial, 0);
                RenderTexture.active = target;
                readback = new Texture2D(1, 1, TextureFormat.RGBA32, false, true)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                readback.ReadPixels(new Rect(0, 0, 1, 1), 0, 0, false);
                readback.Apply(false, false);
                Color pixel = readback.GetPixel(0, 0);
                check(pixel.r <= 0.01f && pixel.g <= 0.01f && pixel.b <= 0.01f && pixel.a >= 0.99f, "unbound default-white mask renders opaque black on GPU");
                check(ReferenceEquals(sharedMaterial.GetTexture("_MainTex"), sharedTextureBefore) && Approximately(sharedMaterial.GetFloat("_MaskBound"), sharedMaskBoundBefore) && Approximately(sharedMaterial.GetFloat("_CompletionFill"), sharedCompletionFillBefore), "unbound GPU validation leaves the shared material unchanged");
            }
            finally
            {
                RenderTexture.active = previous;
                if (readback) UnityEngine.Object.DestroyImmediate(readback);
                if (runtimeMaterial) UnityEngine.Object.DestroyImmediate(runtimeMaterial);
                if (target)
                {
                    if (target.IsCreated()) target.Release();
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }

        private static void ValidatePresenterDestroyOwnership(Action<bool, string> check, Material sharedMaterial)
        {
            GameObject owner = new GameObject("Phase2Stage5B_PresenterOwnership", typeof(RectTransform), typeof(RawImage));
            RenderTexture externalMask = null;
            Material runtimeMaterial = null;
            Texture sharedTextureBefore = sharedMaterial.GetTexture("_MainTex");
            float sharedMaskBoundBefore = sharedMaterial.GetFloat("_MaskBound");
            try
            {
                RawImage cover = owner.GetComponent<RawImage>();
                cover.material = sharedMaterial;
                Phase2MaskPresenter presenter = owner.AddComponent<Phase2MaskPresenter>();
                SetReferences(presenter, ("cover", cover), ("materialTemplate", sharedMaterial));
                externalMask = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!externalMask.Create()) throw new InvalidOperationException("[Phase2Stage5B][Test] presenter ownership mask creation failed");
                check(presenter.Bind(externalMask), "presenter ownership fixture binds an external mask");
                runtimeMaterial = GetRuntimeMaterial(presenter);
                check(runtimeMaterial && !ReferenceEquals(runtimeMaterial, sharedMaterial) && ReferenceEquals(cover.material, runtimeMaterial), "presenter ownership fixture uses a distinct runtime material");
                presenter.ReleaseBinding();
                check(!runtimeMaterial && !GetRuntimeMaterial(presenter) && !cover.texture && ReferenceEquals(cover.material, sharedMaterial), "explicit release destroys the runtime material and restores the graphic binding");
                check(ReferenceEquals(sharedMaterial.GetTexture("_MainTex"), sharedTextureBefore) && Approximately(sharedMaterial.GetFloat("_MaskBound"), sharedMaskBoundBefore), "explicit release leaves the shared material unchanged");
                check(externalMask.IsCreated(), "explicit release preserves the external mask ownership");
                UnityEngine.Object.DestroyImmediate(owner);
                owner = null;
                check(externalMask.IsCreated() && ReferenceEquals(sharedMaterial.GetTexture("_MainTex"), sharedTextureBefore) && Approximately(sharedMaterial.GetFloat("_MaskBound"), sharedMaskBoundBefore), "destroying the cleaned presenter host preserves external mask ownership and shared material state");
            }
            finally
            {
                if (owner) UnityEngine.Object.DestroyImmediate(owner);
                if (externalMask)
                {
                    if (externalMask.IsCreated()) externalMask.Release();
                    UnityEngine.Object.DestroyImmediate(externalMask);
                }
            }
        }

        private static void ValidatePresenterOnDestroyOwnership(Action<bool, string> check, Material sharedMaterial)
        {
            GameObject owner = new GameObject("Phase2Stage5B_PresenterOnDestroy", typeof(RectTransform), typeof(RawImage));
            RenderTexture externalMask = null;
            Material runtimeMaterial = null;
            Texture sharedTextureBefore = sharedMaterial.GetTexture("_MainTex");
            float sharedMaskBoundBefore = sharedMaterial.GetFloat("_MaskBound");
            try
            {
                RawImage cover = owner.GetComponent<RawImage>();
                cover.material = sharedMaterial;
                Phase2MaskPresenter presenter = owner.AddComponent<Phase2MaskPresenter>();
                SetReferences(presenter, ("cover", cover), ("materialTemplate", sharedMaterial));
                check(owner.activeSelf && owner.activeInHierarchy && presenter.enabled, "presenter OnDestroy fixture host is active");

                externalMask = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGB32)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                if (!externalMask.Create()) throw new InvalidOperationException("[Phase2Stage5B][Test] presenter OnDestroy mask creation failed");
                check(presenter.Bind(externalMask), "presenter OnDestroy fixture binds an external mask");
                runtimeMaterial = GetRuntimeMaterial(presenter);
                check(runtimeMaterial && !ReferenceEquals(runtimeMaterial, sharedMaterial) && ReferenceEquals(cover.material, runtimeMaterial), "presenter OnDestroy fixture has a live runtime material");

                MethodInfo onDestroy = typeof(Phase2MaskPresenter).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
                check(onDestroy != null, "presenter OnDestroy lifecycle method is discoverable");
                onDestroy.Invoke(presenter, null);

                check(!runtimeMaterial, "presenter OnDestroy releases its runtime material");
                check(!GetRuntimeMaterial(presenter), "presenter OnDestroy clears the runtime material field");
                check(!cover.texture && ReferenceEquals(cover.material, sharedMaterial), "presenter OnDestroy restores the graphic binding");
                check(ReferenceEquals(sharedMaterial.GetTexture("_MainTex"), sharedTextureBefore) && Approximately(sharedMaterial.GetFloat("_MaskBound"), sharedMaskBoundBefore), "presenter OnDestroy leaves the shared material unchanged");
                check(externalMask.IsCreated(), "presenter OnDestroy preserves the external mask ownership");

                GameObject destroyedOwner = owner;
                UnityEngine.Object.DestroyImmediate(owner);
                owner = null;
                check(!destroyedOwner && externalMask.IsCreated(), "destroying the cleaned presenter host is idempotent");
            }
            finally
            {
                if (owner) UnityEngine.Object.DestroyImmediate(owner);
                if (externalMask)
                {
                    if (externalMask.IsCreated()) externalMask.Release();
                    UnityEngine.Object.DestroyImmediate(externalMask);
                }
            }
        }

        private static Material GetRuntimeMaterial(Phase2MaskPresenter presenter)
        {
            FieldInfo field = typeof(Phase2MaskPresenter).GetField("_runtimeMaterial", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("[Phase2Stage5B][Test] Phase2MaskPresenter._runtimeMaterial field missing");
            return field.GetValue(presenter) as Material;
        }

        private static Phase2PaintOrchestrator GetOwnedOrchestrator(Phase2PhaseAdapter adapter)
        {
            FieldInfo field = typeof(Phase2PhaseAdapter).GetField("_orchestrator", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("[Phase2Stage5B][Test] Phase2PhaseAdapter._orchestrator field missing");
            return field.GetValue(adapter) as Phase2PaintOrchestrator;
        }

        private static void AdvancePresenterCompletion(Phase2MaskPresenter presenter, float unscaledDeltaTime)
        {
            MethodInfo method = typeof(Phase2MaskPresenter).GetMethod("AdvanceCompletion", BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new InvalidOperationException("[Phase2Stage5B][Test] Phase2MaskPresenter.AdvanceCompletion method missing");
            method.Invoke(presenter, new object[] { unscaledDeltaTime });
        }

        private static PointerEventData CreatePointerEvent(int pointerId, Vector2 position)
        {
            var data = new PointerEventData(null)
            {
                pointerId = pointerId,
                position = position
            };
            return data;
        }

        private static GameObject CreateChild(GameObject parent, string name, Type graphicType)
        {
            var child = new GameObject(name, typeof(RectTransform), graphicType);
            child.transform.SetParent(parent.transform, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return child;
        }

        private static void SetReferences(Component target, params (string name, UnityEngine.Object value)[] values)
        {
            var serialized = new SerializedObject(target);
            foreach ((string name, UnityEngine.Object value) in values)
            {
                SerializedProperty property = serialized.FindProperty(name);
                if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + name + " missing.");
                property.objectReferenceValue = value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool RectEquals(RectTransform a, RectTransform b)
        {
            return a && b && a.anchorMin == b.anchorMin && a.anchorMax == b.anchorMax && a.pivot == b.pivot &&
                   a.anchoredPosition == b.anchoredPosition && a.sizeDelta == b.sizeDelta &&
                   a.localScale == b.localScale && a.localRotation == b.localRotation;
        }

        private static bool IsFullStretch(RectTransform rect)
        {
            return rect && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one &&
                   rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
        }

        private static Texture2D ReadMask(RenderTexture source, Texture2D existing)
        {
            if (!existing || existing.width != source.width || existing.height != source.height)
            {
                if (existing) UnityEngine.Object.DestroyImmediate(existing);
                existing = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
                {
                    name = "Phase2Stage5B_Readback",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                existing.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                existing.Apply(false, false);
                return existing;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static byte MaximumRed(Texture2D texture)
        {
            byte maximum = 0;
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) if (pixels[i].r > maximum) maximum = pixels[i].r;
            return maximum;
        }

        private static float SampleRed(Texture2D texture, float u, float v)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(u * (texture.width - 1)), 0, texture.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(v * (texture.height - 1)), 0, texture.height - 1);
            return texture.GetPixel(x, y).r;
        }

        private static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.0001f;
        }
    }
}
#endif
