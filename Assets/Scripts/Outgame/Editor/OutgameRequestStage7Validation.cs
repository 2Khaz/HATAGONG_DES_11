using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HATAGONG.GameFlow;
using HATAGONG.Phase1;
using HATAGONG.Phase3Tangram;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HATAGONG.Outgame.Editor
{
    public static class OutgameRequestStage7Validation
    {
        private const string OutgameScenePath = "Assets/Scenes/OUTGAME_LOBBY.unity";
        private const string IngameSceneName = "INGAME";
        private const string RequestedKey = "HATAGONG.Outgame.Stage7.PlayRequested";

        [InitializeOnLoadMethod]
        private static void Register()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 7 EditMode")]
        public static void ValidateEditMode()
        {
            var validation = new ValidationState();
            var fallback = new GameRunContext(GameDifficulty.Hard, RequestType.Sudden);
            validation.Check(fallback.IsValid, "Fallback Context is valid");
            validation.Check(!fallback.HasSelectedRequest, "Fallback has no selected request");
            validation.Check(fallback.RequestId == string.Empty, "Fallback RequestId is empty");
            validation.Check(fallback.Difficulty == GameDifficulty.Hard, "Fallback keeps Inspector difficulty");
            validation.Check(fallback.RequestType == RequestType.Sudden, "Fallback keeps Inspector request type");
            validation.Check(fallback.PermanentSeed == 0 && fallback.Phase1Seed == 0 && fallback.Phase3Seed == 0, "Fallback has no selected seeds");

            var selected = new GameRunContext("STAGE7", GameDifficulty.Normal, RequestType.Normal, 710001, 710011, 710031, "Img_bigtiles1");
            validation.Check(selected.IsValid && selected.HasSelectedRequest, "Selected Context is valid");
            validation.Check(selected.RequestId == "STAGE7", "Selected RequestId is exact");
            validation.Check(selected.Difficulty == GameDifficulty.Normal, "Selected Difficulty is exact");
            validation.Check(selected.RequestType == RequestType.Normal, "Selected RequestType is exact");
            validation.Check(selected.PermanentSeed == 710001, "Selected PermanentSeed is exact");
            validation.Check(selected.Phase1Seed == 710011, "Selected Phase1Seed is exact");
            validation.Check(selected.Phase3Seed == 710031, "Selected Phase3Seed is exact");
            validation.Check(selected.Phase3ImageKey == "Img_bigtiles1", "Selected Phase3ImageKey is exact");

            validation.Check(!new GameRunContext("", GameDifficulty.Normal, RequestType.Normal, 1, 2, 3, "Img_bigtiles1").IsValid, "Empty RequestId fails closed");
            validation.Check(!new GameRunContext("X", GameDifficulty.Unspecified, RequestType.Normal, 1, 2, 3, "Img_bigtiles1").IsValid, "Invalid Difficulty fails closed");
            validation.Check(!new GameRunContext("X", GameDifficulty.Normal, (RequestType)99, 1, 2, 3, "Img_bigtiles1").IsValid, "Invalid RequestType fails closed");
            validation.Check(!new GameRunContext("X", GameDifficulty.Normal, RequestType.Normal, 0, 2, 3, "Img_bigtiles1").IsValid, "Invalid PermanentSeed fails closed");
            validation.Check(!new GameRunContext("X", GameDifficulty.Normal, RequestType.Normal, 1, 0, 3, "Img_bigtiles1").IsValid, "Invalid Phase1Seed fails closed");
            validation.Check(!new GameRunContext("X", GameDifficulty.Normal, RequestType.Normal, 1, 2, 0, "Img_bigtiles1").IsValid, "Invalid Phase3Seed fails closed");
            validation.Check(!new GameRunContext("X", GameDifficulty.Normal, RequestType.Normal, 1, 2, 3, "").IsValid, "Missing Phase3ImageKey fails closed");

            OutgameRequestOffer retryOffer = CreateOffer(GameDifficulty.Normal, RequestType.Sudden, 715001);
            var retryContext = new GameRunContext(retryOffer.Definition.RequestId, retryOffer.Definition.Difficulty,
                retryOffer.Definition.RequestType, retryOffer.Definition.PermanentSeed,
                retryOffer.Phase1Seed, retryOffer.Phase3Seed, retryOffer.Phase3ImageKey);
            OutgameRequestSelectionStore.SetPending(retryOffer);
            validation.Check(OutgameRequestSelectionStore.ActivatePending() && OutgameRequestSelectionStore.HasActive,
                "Selected offer becomes active retry snapshot");
            validation.Check(OutgameRequestSelectionStore.TryPrepareRetry(retryContext) && OutgameRequestSelectionStore.HasPending,
                "Retry restores the identical active selection to Pending");
            validation.Check(OutgameRequestSelectionStore.TryGetPending(out OutgameRequestRunSelection retrySelection) &&
                ReferenceEquals(retrySelection.OfferSnapshot, retryOffer), "Retry preserves the exact offer snapshot object");
            OutgameRequestSelectionStore.Clear();

            foreach (GameDifficulty difficulty in new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard })
            {
                TangramGenerationResult first = Phase3TangramGenerator.Generate(difficulty, 710031);
                TangramGenerationResult second = Phase3TangramGenerator.Generate(difficulty, 710031);
                validation.Check(first.Success && second.Success, difficulty + " Phase3 generation succeeds");
                validation.Check(Signature(first) == Signature(second), difficulty + " Phase3 selected seed is deterministic");
            }

            validation.Check(typeof(Phase1PhaseAdapter).GetProperty("RunContext") != null, "Phase1 preserves common Context");
            validation.Check(typeof(Phase2PhaseAdapter).GetProperty("RunContext") != null, "Phase2 preserves common Context");
            validation.Check(typeof(Phase3TangramManager).GetProperty("RunContext") != null, "Phase3 preserves common Context");
            validation.Check(typeof(GameRunContext).GetProperty("Phase2Seed") == null, "Common Context has no Phase2Seed");
            Log("EditMode", validation);
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 7 PlayMode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            SessionState.SetBool(RequestedKey, true);
            EditorSceneManager.OpenScene(OutgameScenePath, OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(RequestedKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) RunPlayModeAsync();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseBool(RequestedKey);
            EditorSceneManager.OpenScene(OutgameScenePath, OpenSceneMode.Single);
        }

        private static async void RunPlayModeAsync()
        {
            var validation = new ValidationState();
            try
            {
                OutgameRequestOffer offer = CreateOffer(GameDifficulty.Hard, RequestType.Sudden, 720001);
                OutgameRequestSelectionStore.SetPending(offer);
                validation.Check(OutgameRequestSelectionStore.HasPending, "Pending exists immediately before INGAME transition");
                AsyncOperation load = SceneManager.LoadSceneAsync(IngameSceneName, LoadSceneMode.Single);
                while (load != null && !load.isDone) await Task.Delay(25);
                await Task.Delay(100);

                GameSessionController session = null;
                float deadline = Time.realtimeSinceStartup + 10f;
                while (Time.realtimeSinceStartup < deadline)
                {
                    session = UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
                    if (session && session.RunContext.HasSelectedRequest) break;
                    await Task.Delay(50);
                }

                validation.Check(session != null, "INGAME GameSession exists");
                GameRunContext context = session != null ? session.RunContext : default;
                validation.Check(context.IsValid && context.HasSelectedRequest, "GameSession owns selected Context");
                validation.Check(context.RequestId == offer.Definition.RequestId, "RequestId copied exactly");
                validation.Check(context.Difficulty == offer.Definition.Difficulty, "Difficulty copied exactly");
                validation.Check(context.RequestType == offer.Definition.RequestType, "RequestType copied exactly");
                validation.Check(context.PermanentSeed == offer.Definition.PermanentSeed, "PermanentSeed copied exactly");
                validation.Check(context.Phase1Seed == offer.Phase1Seed, "Phase1Seed copied exactly");
                validation.Check(context.Phase3Seed == offer.Phase3Seed, "Phase3Seed copied exactly");
                validation.Check(context.Phase3ImageKey == offer.Phase3ImageKey, "Phase3ImageKey copied exactly");
                validation.Check(!OutgameRequestSelectionStore.HasPending, "Pending is cleared after Context creation");

                GameRequestContext request = UnityEngine.Object.FindFirstObjectByType<GameRequestContext>();
                validation.Check(request && request.CurrentRequestType == RequestType.Sudden, "Existing Request Context receives Sudden");
                Phase1PhaseAdapter phase1 = UnityEngine.Object.FindFirstObjectByType<Phase1PhaseAdapter>(FindObjectsInactive.Include);
                Phase1BoardController board = UnityEngine.Object.FindFirstObjectByType<Phase1BoardController>(FindObjectsInactive.Include);
                validation.Check(phase1 && phase1.Prepare(context) && phase1.RunContext.RequestId == context.RequestId, "Phase1 receives common Context");
                validation.Check(board && board.CurrentSeed == context.Phase1Seed, "Phase1 uses selected seed exactly");
                string phase1Signature = board ? board.CurrentLayoutHash + ":" + board.CurrentVariantHash : string.Empty;

                Phase2PhaseAdapter phase2 = UnityEngine.Object.FindFirstObjectByType<Phase2PhaseAdapter>(FindObjectsInactive.Include);
                validation.Check(phase2 && phase2.Prepare(context), "Phase2 prepares from common Context");
                validation.Check(phase2 && phase2.RunContext.RequestId == context.RequestId, "Phase2 preserves common Context identity");
                Phase3TangramManager phase3 = UnityEngine.Object.FindFirstObjectByType<Phase3TangramManager>(FindObjectsInactive.Include);
                validation.Check(phase3 && phase3.Prepare(context), "Phase3 prepares from common Context");
                validation.Check(phase3 && phase3.ActiveSeed == context.Phase3Seed, "Phase3 uses selected seed exactly");
                validation.Check(phase3 && phase3.ActiveImageKey == context.Phase3ImageKey, "Phase3 uses selected image key exactly");
                validation.Check(phase3 && phase3.RunContext.RequestType == context.RequestType, "Phase3 preserves RequestType");

                await ValidatePhase1Replay(validation, offer, phase1Signature);
                await ValidateSelectedReload(validation, GameDifficulty.Easy, RequestType.Normal, 730001, "Easy Normal");
                await ValidateSelectedReload(validation, GameDifficulty.Normal, RequestType.Normal, 740001, "Normal Normal");
                await ValidateDirectFallbackReload(validation);
            }
            catch (Exception exception)
            {
                validation.Fail("Unexpected PlayMode exception: " + exception);
            }
            finally
            {
                Log("PlayMode", validation);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        private static async Task ValidatePhase1Replay(ValidationState validation, OutgameRequestOffer offer, string expectedSignature)
        {
            OutgameRequestSelectionStore.SetPending(offer);
            AsyncOperation load = SceneManager.LoadSceneAsync(IngameSceneName, LoadSceneMode.Single);
            while (load != null && !load.isDone) await Task.Delay(25);
            await Task.Delay(100);
            GameSessionController session = UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
            GameRunContext context = session != null ? session.RunContext : default;
            Phase1PhaseAdapter phase1 = UnityEngine.Object.FindFirstObjectByType<Phase1PhaseAdapter>(FindObjectsInactive.Include);
            Phase1BoardController board = UnityEngine.Object.FindFirstObjectByType<Phase1BoardController>(FindObjectsInactive.Include);
            validation.Check(context.RequestId == offer.Definition.RequestId && context.Phase1Seed == offer.Phase1Seed, "Replay keeps identical Phase1 selection");
            validation.Check(phase1 && phase1.Prepare(context) && board && board.CurrentSeed == offer.Phase1Seed, "Replay injects identical Phase1 seed");
            validation.Check(board && board.CurrentLayoutHash + ":" + board.CurrentVariantHash == expectedSignature, "Replay keeps identical Phase1 signature");
        }

        private static async Task ValidateSelectedReload(ValidationState validation, GameDifficulty difficulty,
            RequestType requestType, int permanentSeed, string label)
        {
            OutgameRequestOffer offer = CreateOffer(difficulty, requestType, permanentSeed);
            OutgameRequestSelectionStore.SetPending(offer);
            validation.Check(OutgameRequestSelectionStore.HasPending, label + " Pending exists before reload");
            AsyncOperation load = SceneManager.LoadSceneAsync(IngameSceneName, LoadSceneMode.Single);
            while (load != null && !load.isDone) await Task.Delay(25);
            await Task.Delay(100);

            GameSessionController session = UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
            GameRunContext context = session != null ? session.RunContext : default;
            validation.Check(session && context.HasSelectedRequest && context.Difficulty == difficulty, label + " Context difficulty");
            validation.Check(context.RequestType == requestType && context.RequestId == offer.Definition.RequestId, label + " Context request identity");
            validation.Check(context.Phase1Seed == offer.Phase1Seed && context.Phase3Seed == offer.Phase3Seed && context.Phase3ImageKey == offer.Phase3ImageKey, label + " Context image and seeds");
            validation.Check(!OutgameRequestSelectionStore.HasPending, label + " Pending consumed");

            GameRequestContext request = UnityEngine.Object.FindFirstObjectByType<GameRequestContext>();
            validation.Check(request && request.CurrentRequestType == requestType, label + " RequestPresenter source");
            Phase1PhaseAdapter phase1 = UnityEngine.Object.FindFirstObjectByType<Phase1PhaseAdapter>(FindObjectsInactive.Include);
            Phase1BoardController board = UnityEngine.Object.FindFirstObjectByType<Phase1BoardController>(FindObjectsInactive.Include);
            validation.Check(phase1 && phase1.Prepare(context) && board && board.CurrentSeed == context.Phase1Seed, label + " Phase1 exact seed");
            Phase3TangramManager phase3 = UnityEngine.Object.FindFirstObjectByType<Phase3TangramManager>(FindObjectsInactive.Include);
            validation.Check(phase3 && phase3.Prepare(context) && phase3.ActiveSeed == context.Phase3Seed, label + " Phase3 exact seed");
        }

        private static async Task ValidateDirectFallbackReload(ValidationState validation)
        {
            OutgameRequestSelectionStore.Clear();
            AsyncOperation load = SceneManager.LoadSceneAsync(IngameSceneName, LoadSceneMode.Single);
            while (load != null && !load.isDone) await Task.Delay(25);
            await Task.Delay(100);

            GameSessionController session = UnityEngine.Object.FindFirstObjectByType<GameSessionController>();
            GameRunContext context = session != null ? session.RunContext : default;
            validation.Check(session && context.IsValid && !context.HasSelectedRequest, "Direct INGAME uses fallback Context");
            validation.Check(context.Difficulty == GameDifficulty.Hard, "Direct INGAME keeps Inspector difficulty");
            validation.Check(context.RequestType == RequestType.Normal, "Direct INGAME keeps Request Context fallback");
            validation.Check(context.Phase1Seed == 0 && context.Phase3Seed == 0 && context.Phase3ImageKey == string.Empty, "Direct INGAME has no selected image or seeds");
            validation.Check(!OutgameRequestSelectionStore.HasPending, "Direct INGAME does not create Pending");

            Phase1PhaseAdapter phase1 = UnityEngine.Object.FindFirstObjectByType<Phase1PhaseAdapter>(FindObjectsInactive.Include);
            Phase1BoardController board = UnityEngine.Object.FindFirstObjectByType<Phase1BoardController>(FindObjectsInactive.Include);
            validation.Check(phase1 && phase1.Prepare(context) && board && board.CurrentSeed != 0, "Direct INGAME keeps Phase1 internal seed fallback");
            Phase3TangramManager phase3 = UnityEngine.Object.FindFirstObjectByType<Phase3TangramManager>(FindObjectsInactive.Include);
            validation.Check(phase3 && phase3.Prepare(context) && phase3.ActiveSeed != 0L, "Direct INGAME keeps Phase3 internal seed fallback");
        }

        private static OutgameRequestOffer CreateOffer(GameDifficulty difficulty, RequestType requestType, int permanentSeed)
        {
            var definition = new OutgameRequestDefinition("STAGE7_PLAY", true, requestType, difficulty, permanentSeed,
                permanentSeed + 10, permanentSeed + 30, "Img_bigtiles1",
                "Fixture", "fixture", "Fixture", "Fixture", Array.Empty<OutgameRequestEffectDefinition>());
            ConstructorInfo constructor = typeof(OutgameRequestCatalog).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(IEnumerable<OutgameRequestDefinition>), typeof(IEnumerable<OutgameRequestEffectDefinition>) }, null);
            var catalog = (OutgameRequestCatalog)constructor.Invoke(new object[] { new[] { definition }, Array.Empty<OutgameRequestEffectDefinition>() });
            return OutgameRequestOfferGenerator.Generate(catalog, 20260716).Batch.Offers[0];
        }

        private static string Signature(TangramGenerationResult result) => string.Join("|", result.Pieces.Select(piece =>
            piece.Id + ":" + piece.InitialRotationStep + ":" + string.Join(",", piece.AbsolutePolygon.Select(point => point.x + "/" + point.y))));

        private static void Log(string mode, ValidationState validation)
        {
            string message = $"[Outgame][Stage7][{mode}] result={validation.Passed}/{validation.Total}, failures={validation.Failures.Count}";
            if (validation.Failures.Count == 0) Debug.Log(message);
            else
            {
                Debug.LogError(message);
                for (int i = 0; i < validation.Failures.Count; i++) Debug.LogError($"[Outgame][Stage7][{mode}][Failure {i + 1}] {validation.Failures[i]}");
            }
        }

        private sealed class ValidationState
        {
            private readonly List<string> failures = new List<string>();
            public int Passed { get; private set; }
            public int Total { get; private set; }
            public IReadOnlyList<string> Failures => failures;
            public void Check(bool condition, string name) { Total++; if (condition) Passed++; else failures.Add(name); }
            public void Fail(string name) { failures.Add(name); }
        }
    }
}
