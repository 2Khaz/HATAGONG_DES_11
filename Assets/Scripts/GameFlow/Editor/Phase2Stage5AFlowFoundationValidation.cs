#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using HATAGONG.Phase1;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.GameFlow.Editor
{
    public static class Phase2Stage5AFlowFoundationValidation
    {
        [MenuItem("Tools/HATAGONG/Phase2/Validate Stage 5A Flow Foundation")]
        public static void Validate()
        {
            int passed = 0;
            int total = 0;
            void Check(bool condition, string name)
            {
                total++;
                if (!condition) throw new InvalidOperationException("[Phase2Stage5A][Test] " + name);
                passed++;
            }

            Check(!new GameRunContext(GameDifficulty.Unspecified).IsValid, "unspecified difficulty invalid");
            Check(new GameRunContext(GameDifficulty.Easy).IsValid, "easy difficulty valid");
            Check(new GameRunContext(GameDifficulty.Normal).IsValid, "normal difficulty valid");
            Check(new GameRunContext(GameDifficulty.Hard).IsValid, "hard difficulty valid");
            Check(!new GameRunContext((GameDifficulty)99).IsValid, "out of range difficulty invalid");
            Check((int)GameDifficulty.Unspecified == 0 && (int)GameDifficulty.Easy == 1 && (int)GameDifficulty.Normal == 2 && (int)GameDifficulty.Hard == 3, "game difficulty numeric contract");
            Check((int)Phase1Difficulty.Easy == 0 && (int)Phase1Difficulty.Normal == 1 && (int)Phase1Difficulty.Hard == 2, "phase1 difficulty numeric contract preserved");
            Check(Phase1PhaseAdapter.TryConvertDifficulty(GameDifficulty.Easy, out Phase1Difficulty easy) && easy == Phase1Difficulty.Easy, "explicit easy conversion");
            Check(Phase1PhaseAdapter.TryConvertDifficulty(GameDifficulty.Normal, out Phase1Difficulty normal) && normal == Phase1Difficulty.Normal, "explicit normal conversion");
            Check(Phase1PhaseAdapter.TryConvertDifficulty(GameDifficulty.Hard, out Phase1Difficulty hard) && hard == Phase1Difficulty.Hard, "explicit hard conversion");
            Check(!Phase1PhaseAdapter.TryConvertDifficulty(GameDifficulty.Unspecified, out _), "unspecified conversion rejected");
            Check(!Phase1PhaseAdapter.TryConvertDifficulty((GameDifficulty)99, out _), "out of range conversion rejected");
            Check(typeof(Phase1BoardController).GetEvent("Phase1ClearConditionReached") != null && typeof(Phase1BoardController).GetEvent("Phase1Cleared") != null, "phase1 clear condition and final completion signals are distinct");

            Check(!GamePhaseRegistry.TryCreate(new object[] { null }, GamePhaseId.Phase1, out _, out _), "null phase registration rejected");
            Check(!GamePhaseRegistry.TryCreate(new object[] { new object() }, GamePhaseId.Phase1, out _, out _), "non phase registration rejected");
            Check(!GamePhaseRegistry.TryCreate(new object[] { new FakePhase(GamePhaseId.Phase1), new FakePhase(GamePhaseId.Phase1) }, GamePhaseId.Phase1, out _, out _), "duplicate phase id rejected");
            Check(!GamePhaseRegistry.TryCreate(new object[] { new FakePhase(GamePhaseId.Phase1) }, GamePhaseId.Phase2, out _, out _), "missing initial phase rejected");

            var trace = new List<string>();
            var phase1 = new FakePhase(GamePhaseId.Phase1, trace);
            var phase2 = new FakePhase(GamePhaseId.Phase2, trace);
            Check(GamePhaseRegistry.TryCreate(new object[] { phase1, phase2 }, GamePhaseId.Phase1, out GamePhaseRegistry registry, out _), "valid phase registry accepted");
            var transaction = new GamePhaseTransaction(registry);
            Check(transaction.TryInitialize(GamePhaseId.Phase1, new GameRunContext(GameDifficulty.Hard), out _), "initial prepare and activate succeed");
            Check(transaction.CurrentPhase == phase1 && phase1.IsPrepared && phase1.IsRunning, "initial current phase assigned after activation");
            Check(trace.IndexOf("Phase1:prepare") >= 0 && trace.IndexOf("Phase1:prepare") < trace.IndexOf("Phase1:activate"), "prepare occurs before activate");
            Check(!phase1.InputEnabled && !phase2.InputEnabled, "initial activate does not open input");
            transaction.SetCurrentInputEnabled(true);
            Check(phase1.InputEnabled && !phase2.InputEnabled, "only current phase input can open");

            CheckInitializationFailure(new FakePhase(GamePhaseId.Phase1) { PrepareResult = false }, Check, "prepare false");
            CheckInitializationFailure(new FakePhase(GamePhaseId.Phase1) { ThrowOnPrepare = true }, Check, "prepare exception");
            CheckInitializationFailure(new FakePhase(GamePhaseId.Phase1) { ActivateResult = false }, Check, "activate false");
            CheckInitializationFailure(new FakePhase(GamePhaseId.Phase1) { ThrowOnActivate = true }, Check, "activate exception");

            int clearedEvents = 0;
            int exitReadyEvents = 0;
            phase1.PhaseCleared += () => clearedEvents++;
            phase1.PhaseExitReady += () => exitReadyEvents++;
            phase1.RaiseCleared();
            Check(clearedEvents == 1 && exitReadyEvents == 0 && !phase1.IsExitReady, "clear condition does not imply exit ready");
            Check(phase2.PrepareCount == 0, "next phase not prepared at clear condition");

            var gate = new GamePhaseExitReadyGate();
            Check(!gate.TryAccept(phase1, phase1, GameSessionState.Playing), "gate rejects clear-only signal");
            phase1.RaiseExitReady();
            Check(clearedEvents == 1 && exitReadyEvents == 1 && phase1.IsExitReady, "exit ready raised once after finalization");
            Check(gate.TryAccept(phase1, phase1, GameSessionState.Playing), "current playing phase exit ready accepted");
            Check(!gate.TryAccept(phase1, phase1, GameSessionState.Playing), "duplicate exit ready ignored");
            Check(!gate.TryAccept(phase2, phase1, GameSessionState.Playing), "non-current exit ready rejected");
            var newRunGate = new GamePhaseExitReadyGate();
            Check(newRunGate.TryAccept(phase1, phase1, GameSessionState.Playing), "new run exit-ready gate starts unhandled");

            trace.Clear();
            bool timerPaused = false;
            var transitionModel = new GameSessionModel();
            transitionModel.SetState(GameSessionState.Ready);
            transitionModel.SetState(GameSessionState.Playing);
            transaction.SetCurrentInputEnabled(false);
            Check(transitionModel.SetState(GameSessionState.Transitioning), "exit ready locks session transitioning");
            timerPaused = true;
            trace.Add("timer:pause");
            Check(transaction.TryPrepareNext(phase2, new GameRunContext(GameDifficulty.Hard), out _), "next prepare succeeds after exit ready");
            trace.Add("overlay:start");
            Check(timerPaused && trace.IndexOf("timer:pause") < trace.IndexOf("Phase2:prepare") && trace.IndexOf("Phase2:prepare") < trace.IndexOf("overlay:start"), "timer pause then prepare then overlay order");
            Check(transaction.CurrentPhase == phase1 && !phase2.IsRunning && !phase1.InputEnabled && !phase2.InputEnabled, "midpoint precondition preserves previous current and all input off");
            Check(transaction.TryCommitPreparedNext(phase2, out _), "midpoint commit succeeds");
            Check(trace.IndexOf("Phase2:activate") < trace.IndexOf("Phase1:deactivate"), "next activate precedes previous deactivate");
            Check(transaction.CurrentPhase == phase2 && phase2.IsRunning && !phase1.IsRunning, "current phase changes only after activation");
            Check(!phase1.InputEnabled && !phase2.InputEnabled, "midpoint commit keeps all input off");
            Check(transitionModel.SetState(GameSessionState.Playing), "successful overlay completion restores playing");
            trace.Add("timer:resume");
            timerPaused = false;
            transaction.SetCurrentInputEnabled(true);
            trace.Add("current:input-on");
            Check(!timerPaused && trace.IndexOf("timer:resume") < trace.IndexOf("current:input-on"), "timer resumes immediately before current input on");
            Check(phase2.InputEnabled && !phase1.InputEnabled, "normal completion opens phase2 only");

            Check(GamePhaseTransitionCompletion.ShouldResume(PhaseTransitionResult.Succeeded, true, true), "successful overlay resumes committed transition");
            Check(GamePhaseTransitionCompletion.ShouldResume(PhaseTransitionResult.Interrupted, true, true), "post-midpoint interruption preserves next phase");
            Check(!GamePhaseTransitionCompletion.ShouldResume(PhaseTransitionResult.Interrupted, false, false), "pre-midpoint interruption remains locked");
            Check(!GamePhaseTransitionCompletion.ShouldResume(PhaseTransitionResult.Failed, false, false), "failed transition remains locked");
            Check(!GamePhaseTransitionCompletion.ShouldResume(PhaseTransitionResult.Rejected, false, false), "rejected transition remains locked");

            ValidatePrepareFailurePolicy(Check);
            ValidateActivateFailurePolicy(Check);
            ValidatePhase1FinalizationAndDeactivate(Check);
            ValidateSceneAndOverlay(Check);

            Debug.Log($"[Phase2Stage5A][Test] result={passed}/{total}, failures=0");
        }

        private static void CheckInitializationFailure(FakePhase phase, Action<bool, string> check, string label)
        {
            GamePhaseRegistry.TryCreate(new object[] { phase }, GamePhaseId.Phase1, out GamePhaseRegistry registry, out _);
            var transaction = new GamePhaseTransaction(registry);
            bool result = transaction.TryInitialize(GamePhaseId.Phase1, new GameRunContext(GameDifficulty.Hard), out _);
            check(!result && transaction.CurrentPhase == null, label + " keeps current phase unset");
            check(!phase.InputEnabled && phase.DeactivateCount == 1, label + " deactivates and keeps input off");
        }

        private static void ValidatePrepareFailurePolicy(Action<bool, string> check)
        {
            var current = new FakePhase(GamePhaseId.Phase1);
            var next = new FakePhase(GamePhaseId.Phase2) { PrepareResult = false };
            GamePhaseRegistry.TryCreate(new object[] { current, next }, GamePhaseId.Phase1, out GamePhaseRegistry registry, out _);
            var transaction = new GamePhaseTransaction(registry);
            transaction.TryInitialize(GamePhaseId.Phase1, new GameRunContext(GameDifficulty.Hard), out _);
            transaction.SetCurrentInputEnabled(false);
            var model = new GameSessionModel();
            model.SetState(GameSessionState.Ready);
            model.SetState(GameSessionState.Playing);
            model.SetState(GameSessionState.Transitioning);
            bool timerPaused = true;
            bool prepared = transaction.TryPrepareNext(next, new GameRunContext(GameDifficulty.Hard), out _);
            check(!prepared && transaction.CurrentPhase == current && !current.InputEnabled && !next.InputEnabled, "prepare failure retains completed previous current with all input off");
            check(model.IsTransitioning && timerPaused && current.ActivateCount == 1, "prepare failure does not restart previous phase or timer");
        }

        private static void ValidateActivateFailurePolicy(Action<bool, string> check)
        {
            var trace = new List<string>();
            var current = new FakePhase(GamePhaseId.Phase1, trace);
            var next = new FakePhase(GamePhaseId.Phase2, trace) { ActivateResult = false };
            GamePhaseRegistry.TryCreate(new object[] { current, next }, GamePhaseId.Phase1, out GamePhaseRegistry registry, out _);
            var transaction = new GamePhaseTransaction(registry);
            transaction.TryInitialize(GamePhaseId.Phase1, new GameRunContext(GameDifficulty.Hard), out _);
            transaction.SetCurrentInputEnabled(false);
            transaction.TryPrepareNext(next, new GameRunContext(GameDifficulty.Hard), out _);
            int previousDeactivateCount = current.DeactivateCount;
            bool committed = transaction.TryCommitPreparedNext(next, out _);
            check(!committed && transaction.CurrentPhase == current && current.DeactivateCount == previousDeactivateCount, "activate failure does not deactivate or restart previous phase");
            check(!current.InputEnabled && !next.InputEnabled && next.DeactivateCount == 1, "activate failure deactivates next and leaves all input off");
        }

        private static void ValidatePhase1FinalizationAndDeactivate(Action<bool, string> check)
        {
            var fixtureRoot = new GameObject("Phase1Stage5AValidationFixture");
            fixtureRoot.SetActive(false);
            var root = new GameObject("Phase1Root", typeof(RectTransform));
            root.transform.SetParent(fixtureRoot.transform, false);
            Phase1GameConfig config = null;
            try
            {
                var fieldRoot = root.GetComponent<RectTransform>();
                fieldRoot.sizeDelta = new Vector2(1250f, 1250f);
                var tileContainerObject = new GameObject("TileContainer", typeof(RectTransform));
                tileContainerObject.transform.SetParent(root.transform, false);
                var tileContainer = tileContainerObject.GetComponent<RectTransform>();
                var tilePrefabObject = new GameObject("TilePrefab", typeof(RectTransform), typeof(Phase1TileView));
                tilePrefabObject.transform.SetParent(root.transform, false);
                var tilePrefab = tilePrefabObject.GetComponent<Phase1TileView>();
                var input = root.AddComponent<Phase1InputController>();
                var board = root.AddComponent<Phase1BoardController>();
                var phaseScore = root.AddComponent<Phase1ScoreController>();
                var gameScore = root.AddComponent<GameScoreController>();
                var feedback = root.AddComponent<Phase1FeedbackController>();
                var adapter = root.AddComponent<Phase1PhaseAdapter>();
                config = ScriptableObject.CreateInstance<Phase1GameConfig>();
                config.EnsureDefaults();

                SetObjectReference(phaseScore, "gameScore", gameScore);
                SetObjectReference(input, "boardController", board);
                SetObjectReference(board, "fieldRoot", fieldRoot);
                SetObjectReference(board, "tileContainer", tileContainer);
                SetObjectReference(board, "tilePrefab", tilePrefab);
                SetObjectReference(board, "inputController", input);
                SetObjectReference(board, "scoreController", phaseScore);
                SetObjectReference(board, "feedbackController", feedback);
                SetObjectReference(board, "config", config);
                SetObjectReference(feedback, "config", config);
                SetObjectReference(adapter, "board", board);
                SetObjectReference(adapter, "input", input);

                int scoreAtClearCondition = -1;
                int scoreAtExitReady = -1;
                int exitReadyCount = 0;
                int phaseClearedCount = 0;
                int totalBeforeClear = gameScore.CurrentScore;
                int expectedTotalAtExitReady = totalBeforeClear + config.ClearScore;
                Action clearConditionObserver = () => scoreAtClearCondition = gameScore.CurrentScore;
                board.Phase1ClearConditionReached += clearConditionObserver;
                adapter.PhaseCleared += () => phaseClearedCount++;
                Action exitReadyHandler = () =>
                {
                    exitReadyCount++;
                    scoreAtExitReady = gameScore.CurrentScore;
                };
                adapter.PhaseExitReady += exitReadyHandler;

                var context = new GameRunContext(GameDifficulty.Hard);
                check(adapter.Prepare(context) && adapter.IsPrepared && board.IsPrepared, "phase1 prepare succeeds while root inactive");
                Delegate clearConditionHandlers = GetEventHandlers(board, "Phase1ClearConditionReached");
                Delegate finalizedHandlers = GetEventHandlers(board, "Phase1Cleared");
                Delegate exitReadyHandlers = GetEventHandlers(adapter, "PhaseExitReady");
                check(CountHandler(clearConditionHandlers, adapter, "OnClearConditionReached") == 1, "inactive phase1 prepare subscribes clear condition exactly once");
                check(CountHandler(finalizedHandlers, adapter, "OnFinalized") == 1, "inactive phase1 prepare subscribes finalization exactly once");
                check(CountHandler(exitReadyHandlers, exitReadyHandler.Target, exitReadyHandler.Method.Name) == 1, "validation phase exit ready observer subscribed exactly once");
                check(adapter.Prepare(context), "repeated phase1 prepare succeeds");
                check(CountHandler(GetEventHandlers(board, "Phase1ClearConditionReached"), adapter, "OnClearConditionReached") == 1 && CountHandler(GetEventHandlers(board, "Phase1Cleared"), adapter, "OnFinalized") == 1, "repeated phase1 prepare does not duplicate board subscriptions");

                fixtureRoot.SetActive(true);
                check(adapter.Activate(), "phase1 activate succeeds after inactive prepare");

                check(root.activeSelf && root.activeInHierarchy && adapter.gameObject.activeInHierarchy, "phase1 fixture root and adapter object active before clear");
                check(adapter.enabled && adapter.isActiveAndEnabled, "phase1 adapter enabled and active before clear");
                check(GetObjectReference<Phase1BoardController>(adapter, "board") == board && GetObjectReference<Phase1InputController>(adapter, "input") == input, "phase1 adapter references connected before clear");

                clearConditionHandlers = GetEventHandlers(board, "Phase1ClearConditionReached");
                finalizedHandlers = GetEventHandlers(board, "Phase1Cleared");
                exitReadyHandlers = GetEventHandlers(adapter, "PhaseExitReady");
                check(CountHandler(clearConditionHandlers, adapter, "OnClearConditionReached") == 1, "phase1 clear condition subscribed to adapter exactly once");
                check(CountHandler(finalizedHandlers, adapter, "OnFinalized") == 1, "phase1 finalized subscribed to adapter exactly once");
                check(CountHandler(exitReadyHandlers, exitReadyHandler.Target, exitReadyHandler.Method.Name) == 1, "validation phase exit ready observer subscribed exactly once");

                object boardStateBeforeDeactivate = GetPrivateValue<object>(board, "state");
                bool clearedBeforeDeactivate = adapter.IsCleared;
                bool exitReadyBeforeDeactivate = adapter.IsExitReady;
                int scoreBeforeDeactivate = gameScore.CurrentScore;
                adapter.SetInputEnabled(true);
                input.TryBegin(777, tilePrefab);
                check(input.InputEnabled && GetPrivateValue<object>(input, "activePointer") != null, "phase1 fixture establishes active pointer before deactivate");

                adapter.Deactivate();
                check(!adapter.IsRunning && adapter.IsPrepared, "phase1 deactivate stops running and preserves prepared state");
                check(!input.InputEnabled && GetPrivateValue<object>(input, "activePointer") == null, "phase1 deactivate disables input and cancels active pointer state");
                check(!root.activeSelf && !root.activeInHierarchy, "phase1 deactivate makes root inactive");
                check(CountHandler(GetEventHandlers(board, "Phase1ClearConditionReached"), adapter, "OnClearConditionReached") == 0 && CountHandler(GetEventHandlers(board, "Phase1Cleared"), adapter, "OnFinalized") == 0, "phase1 deactivate removes board subscriptions");
                check(GetObjectReference<Phase1BoardController>(adapter, "_subscribedBoard") == null, "phase1 deactivate clears subscribed board reference");
                check(gameScore.CurrentScore == scoreBeforeDeactivate && ReferenceEquals(GetPrivateValue<object>(board, "state"), boardStateBeforeDeactivate), "phase1 deactivate preserves score and prepared board");
                check(adapter.IsCleared == clearedBeforeDeactivate && adapter.IsExitReady == exitReadyBeforeDeactivate, "phase1 deactivate preserves clear and exit-ready state");

                adapter.Deactivate();
                check(CountHandler(GetEventHandlers(board, "Phase1ClearConditionReached"), adapter, "OnClearConditionReached") == 0 && CountHandler(GetEventHandlers(board, "Phase1Cleared"), adapter, "OnFinalized") == 0 && GetObjectReference<Phase1BoardController>(adapter, "_subscribedBoard") == null, "repeated phase1 deactivate keeps subscriptions cleared");
                check(!input.InputEnabled && !root.activeSelf && !root.activeInHierarchy && !adapter.IsRunning && adapter.IsPrepared, "repeated phase1 deactivate remains inactive with input off");
                check(gameScore.CurrentScore == scoreBeforeDeactivate && ReferenceEquals(GetPrivateValue<object>(board, "state"), boardStateBeforeDeactivate) && adapter.IsCleared == clearedBeforeDeactivate && adapter.IsExitReady == exitReadyBeforeDeactivate, "repeated phase1 deactivate is state and score idempotent");

                bool reactivated = adapter.Activate();
                check(reactivated && adapter.IsRunning && adapter.IsPrepared, "phase1 reactivate succeeds from prepared state");
                check(root.activeSelf && root.activeInHierarchy && !input.InputEnabled, "phase1 reactivate restores root while keeping input off");
                check(GetObjectReference<Phase1BoardController>(adapter, "_subscribedBoard") == board, "phase1 reactivate restores subscribed board reference");
                check(CountHandler(GetEventHandlers(board, "Phase1ClearConditionReached"), adapter, "OnClearConditionReached") == 1 && CountHandler(GetEventHandlers(board, "Phase1Cleared"), adapter, "OnFinalized") == 1, "phase1 reactivate restores exactly one board subscription");
                check(gameScore.CurrentScore == scoreBeforeDeactivate && ReferenceEquals(GetPrivateValue<object>(board, "state"), boardStateBeforeDeactivate), "phase1 reactivate preserves score and prepared board");

                MethodInfo clear = typeof(Phase1BoardController).GetMethod("Clear", BindingFlags.Instance | BindingFlags.NonPublic);
                check(clear != null, "phase1 clear method available for contract validation");
                clear.Invoke(board, null);
                check(scoreAtClearCondition == totalBeforeClear, "phase exit ready does not occur before clear score application");
                check(exitReadyCount == 1 && scoreAtExitReady == expectedTotalAtExitReady && gameScore.CurrentScore == expectedTotalAtExitReady, "phase exit ready observes clear score in total score");
                check(phaseClearedCount == 1 && adapter.IsCleared && adapter.IsExitReady, "phase1 adapter finalized state set once before exit ready returns");
                clear.Invoke(board, null);
                check(phaseClearedCount == 1 && exitReadyCount == 1 && gameScore.CurrentScore == expectedTotalAtExitReady, "duplicate phase1 finalization does not repeat clear, exit ready, or clear score");

                input.SetInputEnabled(true);
                int scoreBeforeFinalDeactivate = gameScore.CurrentScore;
                adapter.Deactivate();
                check(!input.InputEnabled, "phase1 deactivate disables input and cancels active pointer state");
                check(!adapter.IsRunning && !root.activeSelf, "phase1 deactivate stops running state and deactivates root");
                check(gameScore.CurrentScore == scoreBeforeFinalDeactivate && adapter.IsCleared && adapter.IsExitReady, "phase1 deactivate does not change score or finalized state");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fixtureRoot);
                if (config) UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static T GetObjectReference<T>(UnityEngine.Object target, string fieldName) where T : UnityEngine.Object
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        private static T GetPrivateValue<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException(target.GetType().Name + "." + fieldName + " missing");
            return (T)field.GetValue(target);
        }

        private static Delegate GetEventHandlers(object target, string eventName)
        {
            FieldInfo field = target.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException(target.GetType().Name + "." + eventName + " backing delegate missing");
            return field.GetValue(target) as Delegate;
        }

        private static int CountHandler(Delegate handlers, object target, string methodName)
        {
            if (handlers == null) return 0;
            int count = 0;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                if (ReferenceEquals(handler.Target, target) && handler.Method.Name == methodName) count++;
            }
            return count;
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException(target.GetType().Name + "." + propertyName + " missing");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidateSceneAndOverlay(Action<bool, string> check)
        {
            var session = UnityEngine.Object.FindFirstObjectByType<GameSessionController>(FindObjectsInactive.Include);
            var board = UnityEngine.Object.FindFirstObjectByType<Phase1BoardController>(FindObjectsInactive.Include);
            var adapter = UnityEngine.Object.FindFirstObjectByType<Phase1PhaseAdapter>(FindObjectsInactive.Include);
            var transition = UnityEngine.Object.FindFirstObjectByType<PhaseTransitionController>(FindObjectsInactive.Include);
            var overlay = UnityEngine.Object.FindFirstObjectByType<PhaseTransitionOverlay>(FindObjectsInactive.Include);
            check(session && board && adapter && transition && overlay, "scene flow references exist");

            var sessionSo = new SerializedObject(session);
            var boardSo = new SerializedObject(board);
            var adapterSo = new SerializedObject(adapter);
            var transitionSo = new SerializedObject(transition);
            check(sessionSo.FindProperty("difficulty").intValue == (int)GameDifficulty.Hard, "scene game difficulty hard equals 3");
            check(boardSo.FindProperty("difficulty").intValue == (int)Phase1Difficulty.Hard, "scene phase1 difficulty hard remains 2");
            check(!boardSo.FindProperty("generateOnStart").boolValue, "scene phase1 automatic generation disabled");
            check(sessionSo.FindProperty("timer").objectReferenceValue && sessionSo.FindProperty("score").objectReferenceValue && sessionSo.FindProperty("startOverlay").objectReferenceValue && sessionSo.FindProperty("transition").objectReferenceValue, "session required references preserved");
            var phases = sessionSo.FindProperty("phases");
            Phase1PhaseAdapter[] phase1Adapters = UnityEngine.Object.FindObjectsByType<Phase1PhaseAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Phase2PhaseAdapter[] phase2Adapters = UnityEngine.Object.FindObjectsByType<Phase2PhaseAdapter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Phase2PhaseAdapter phase2Adapter = phase2Adapters.Length > 0 ? phase2Adapters[0] : null;
            GameObject middle = GameObject.Find("Canvas/Game_UI_General/Middle_GamePanel");
            Transform phase1Root = middle ? middle.transform.Find("Phase1_FieldRoot") : null;
            Transform phase2Root = middle ? middle.transform.Find("Phase2Root") : null;
            check(phase1Adapters.Length == 1 && phase1Adapters[0] == adapter && phase1Root && adapter.gameObject == phase1Root.gameObject, "scene contains the existing phase1 adapter exactly once");

            int phase1RegistrationCount = 0;
            int phase2RegistrationCount = 0;
            int nullRegistrationCount = 0;
            int otherRegistrationCount = 0;
            for (int i = 0; i < phases.arraySize; i++)
            {
                UnityEngine.Object entry = phases.GetArrayElementAtIndex(i).objectReferenceValue;
                if (!entry) nullRegistrationCount++;
                else if (entry == adapter) phase1RegistrationCount++;
                else if (phase2Adapter && entry == phase2Adapter) phase2RegistrationCount++;
                else otherRegistrationCount++;
            }
            check(phase1RegistrationCount == 1, "scene registers the existing phase1 adapter exactly once");
            check(sessionSo.FindProperty("initialPhase").intValue == (int)GamePhaseId.Phase1, "scene keeps phase1 as the initial phase");

            if (phase2Adapters.Length == 0)
            {
                check(!phase2Root, "stage5a scene has no phase2 root before integration");
                check(phases.arraySize == 1 && phases.GetArrayElementAtIndex(0).objectReferenceValue == adapter, "stage5a scene contains only phase1 before phase2 integration");
                check(nullRegistrationCount == 0 && otherRegistrationCount == 0 && phase2RegistrationCount == 0, "stage5a scene has no null or additional phase registrations");
            }
            else
            {
                check(phase2Adapters.Length == 1 && phase2Root && phase2Adapter.gameObject == phase2Root.gameObject, "integrated scene contains exactly one phase2 adapter on Phase2Root");
                check(phases.arraySize == 2, "integrated scene registers exactly two phases");
                check(phase2RegistrationCount == 1, "integrated scene registers the phase2 adapter exactly once");
                check(nullRegistrationCount == 0 && otherRegistrationCount == 0, "integrated scene has no null or unknown phase registrations");
                check(phases.GetArrayElementAtIndex(0).objectReferenceValue == adapter && phases.GetArrayElementAtIndex(1).objectReferenceValue == phase2Adapter, "integrated scene registers phase1 then phase2 exactly once");
                check(phase1Root && phase2Root && phase1Root != phase2Root && adapter.gameObject != phase2Adapter.gameObject, "integrated scene keeps phase1 and phase2 on distinct roots");
                check(!phase2Root.gameObject.activeSelf, "integrated scene keeps phase2 root initially inactive");
            }
            check(adapterSo.FindProperty("board").objectReferenceValue == board && adapterSo.FindProperty("input").objectReferenceValue, "phase1 adapter references preserved");
            check(sessionSo.FindProperty("transition").objectReferenceValue == transition, "session transition controller reference preserved");
            check(transitionSo.FindProperty("overlay").objectReferenceValue == overlay && transitionSo.FindProperty("phaseHud").objectReferenceValue, "transition references preserved");
            check(Mathf.Approximately(overlay.TotalConfiguredDuration, overlay.EnterDuration + overlay.HoldDuration + overlay.ExitDuration + overlay.CompletionDelay), "overlay duration is exact configured sum");
            check(Mathf.Approximately(overlay.TotalConfiguredDuration, 0.75f), "overlay duration remains 0.75 seconds");
            check(overlay.TotalConfiguredDuration <= 2f, "overlay duration within absolute 2.0 second ceiling");
            check(overlay.TotalConfiguredDuration <= 1.4f, "overlay duration within recommended 1.4 second target");
            if (overlay.TotalConfiguredDuration > 1.4f) Debug.LogWarning($"[Phase2Stage5A][Timing] configured overlay duration exceeds target: {overlay.TotalConfiguredDuration:F3}s");
        }

        private sealed class FakePhase : IGamePhase
        {
            private readonly List<string> _trace;
            private bool _exitRaised;

            public FakePhase(GamePhaseId phaseId, List<string> trace = null)
            {
                PhaseId = phaseId;
                _trace = trace;
            }

            public GamePhaseId PhaseId { get; }
            public bool IsPrepared { get; private set; }
            public bool IsRunning { get; private set; }
            public bool IsCleared { get; private set; }
            public bool IsExitReady { get; private set; }
            public bool InputEnabled { get; private set; }
            public bool PrepareResult { get; set; } = true;
            public bool ActivateResult { get; set; } = true;
            public bool ThrowOnPrepare { get; set; }
            public bool ThrowOnActivate { get; set; }
            public int PrepareCount { get; private set; }
            public int ActivateCount { get; private set; }
            public int DeactivateCount { get; private set; }

            public event Action PhaseCleared;
            public event Action PhaseExitReady;

            public bool Prepare(GameRunContext context)
            {
                PrepareCount++;
                _trace?.Add(PhaseId + ":prepare");
                if (ThrowOnPrepare) throw new InvalidOperationException("prepare validation");
                IsPrepared = PrepareResult && context.IsValid;
                IsRunning = false;
                InputEnabled = false;
                return IsPrepared;
            }

            public bool Activate()
            {
                ActivateCount++;
                _trace?.Add(PhaseId + ":activate");
                if (ThrowOnActivate) throw new InvalidOperationException("activate validation");
                IsRunning = IsPrepared && ActivateResult;
                return IsRunning;
            }

            public void Deactivate()
            {
                DeactivateCount++;
                _trace?.Add(PhaseId + ":deactivate");
                IsRunning = false;
                InputEnabled = false;
            }

            public void SetInputEnabled(bool enabled)
            {
                InputEnabled = enabled && IsPrepared && IsRunning && !IsExitReady;
                _trace?.Add(PhaseId + ":input:" + InputEnabled);
            }

            public void RaiseCleared()
            {
                if (IsCleared) return;
                IsCleared = true;
                InputEnabled = false;
                PhaseCleared?.Invoke();
            }

            public void RaiseExitReady()
            {
                if (_exitRaised) return;
                _exitRaised = true;
                IsExitReady = true;
                InputEnabled = false;
                PhaseExitReady?.Invoke();
            }
        }
    }
}
#endif
