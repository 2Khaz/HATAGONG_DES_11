using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HATAGONG.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HATAGONG.Outgame.Editor
{
    public static class OutgameRequestStage1Validation
    {
        private const string RequestHeader = "RequestId,Enabled,RequestType,Difficulty,PermanentSeed,Phase1Seed,Phase3Seed,Phase3ImageKey,RequesterName,PortraitKey,Title,Description,Effect1Id,Effect2Id,Effect3Id";
        private const string EffectHeader = "EffectId,Enabled,EffectName,EffectIconKey,EffectScriptKey,Description";
        private const string PlayRequestedKey = "HATAGONG.Outgame.Stage1.PlayRequested";
        private const string PlayRunningKey = "HATAGONG.Outgame.Stage1.PlayRunning";
        private const string PlayAwaitingExitKey = "HATAGONG.Outgame.Stage1.PlayAwaitingExit";
        private const string PlayPassedKey = "HATAGONG.Outgame.Stage1.PlayPassed";
        private const string PlayTotalKey = "HATAGONG.Outgame.Stage1.PlayTotal";
        private const string PlayFailuresKey = "HATAGONG.Outgame.Stage1.PlayFailures";
        private const string ScenePathKey = "HATAGONG.Outgame.Stage1.ScenePath";
        private const string SceneDirtyKey = "HATAGONG.Outgame.Stage1.SceneDirty";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 1 EditMode")]
        public static void ValidateEditMode()
        {
            ValidationState validation = RunPureValidation(ReadProjectRequests(), ReadProjectEffects());
            LogResult("EditMode", validation);
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 1 PlayMode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Outgame][Stage1][PlayMode] A Play Mode transition is already active.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            SessionState.SetBool(PlayRequestedKey, true);
            SessionState.SetBool(PlayRunningKey, false);
            SessionState.SetBool(PlayAwaitingExitKey, false);
            SessionState.SetString(ScenePathKey, scene.path ?? string.Empty);
            SessionState.SetBool(SceneDirtyKey, scene.isDirty);
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
            validation.Check(
                string.Equals(scene.path, SessionState.GetString(ScenePathKey, string.Empty), StringComparison.Ordinal),
                "Scene path remains unchanged after Play Mode");
            validation.Check(
                scene.isDirty == SessionState.GetBool(SceneDirtyKey, false),
                "Scene dirty state remains unchanged after Play Mode");

            ClearPlaySession();
            LogResult("PlayMode", validation);
        }

        private static async void RunPlayModeValidationAsync()
        {
            var validation = new ValidationState();
            try
            {
                OutgameRequestTableLoadResult first = await OutgameRequestTableLoader.LoadAsync();
                ValidateLoadedCatalog("First", first, validation);

                OutgameRequestTableLoadResult second = await OutgameRequestTableLoader.LoadAsync();
                ValidateLoadedCatalog("Second", second, validation);
                validation.Check(first.Catalog != null && second.Catalog != null, "Repeated load catalogs exist");
                if (first.Catalog != null && second.Catalog != null)
                {
                    validation.Check(!ReferenceEquals(first.Catalog, second.Catalog), "Repeated load creates independent catalogs");
                    validation.Check(first.Catalog.Requests.Count == second.Catalog.Requests.Count, "Repeated request count stable");
                    validation.Check(first.Catalog.Effects.Count == second.Catalog.Effects.Count, "Repeated effect count stable");
                    validation.Check(
                        first.Catalog.Requests.Select(value => value.RequestId)
                            .SequenceEqual(second.Catalog.Requests.Select(value => value.RequestId)),
                        "Repeated request values stable");
                    validation.Check(
                        first.Catalog.Effects.Select(value => value.EffectId)
                            .SequenceEqual(second.Catalog.Effects.Select(value => value.EffectId)),
                        "Repeated effect values stable");
                    validation.Check(
                        !ReferenceEquals(first.Catalog.Requests[0], second.Catalog.Requests[0]),
                        "Previous result is not reused by next load");
                }

                string effects = ValidEffects();
                ExpectFailure("Play missing EffectId", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", "MISSING", string.Empty, string.Empty)), effects, "does not exist", validation);
                ExpectFailure("Play disabled Effect", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", "FX_OFF", string.Empty, string.Empty)), Effects(EffectRow("FX_OFF", "FALSE")), "disabled", validation);
                ExpectFailure("Play duplicate seed", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1"), RequestRow("B", "TRUE", "Sudden", "Hard", "1")), effects, "already owned", validation);
                ExpectFailure("Play effect gap", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", string.Empty, "FX1", string.Empty)), effects, "left-aligned", validation);
                ExpectFailure("Play bad difficulty", Requests(RequestRow("A", "TRUE", "Normal", "Impossible", "1")), effects, "Difficulty must", validation);
                ExpectFailure("Play duplicate RequestId", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1"), RequestRow("A", "TRUE", "Sudden", "Hard", "2")), effects, "RequestId must be unique", validation);
            }
            catch (Exception exception)
            {
                validation.Check(false, "Unexpected Play Mode exception: " + exception);
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

        private static ValidationState RunPureValidation(string projectRequests, string projectEffects)
        {
            var validation = new ValidationState();
            OutgameRequestTableLoadResult normal = OutgameRequestCatalog.LoadFromCsv(projectRequests, projectEffects);
            validation.Check(normal.Success, "Project CSV succeeds");
            validation.Check(normal.Catalog != null, "Project CSV returns catalog");
            validation.Check(normal.Errors.Count == 0, "Project CSV returns no errors");
            if (normal.Catalog != null)
            {
                OutgameRequestCatalog catalog = normal.Catalog;
                validation.Check(catalog.Requests.Count > 0, "Project request catalog is not empty");
                validation.Check(catalog.Effects.Count == RequestEffectRuntime.SupportedEffectCount, "Project effect count matches runtime contracts");
                validation.Check(catalog.EnabledRequests.Count > 0, "Project enabled request catalog is not empty");
                validation.Check(catalog.Requests.All(request => request.Effects.Count <= 3), "Project requests use at most three effects");
                for (int i = 0; i < RequestEffectRuntime.SupportedEffectIds.Count; i++)
                {
                    string effectId = RequestEffectRuntime.SupportedEffectIds[i];
                    validation.Check(catalog.TryGetEffect(effectId, out OutgameRequestEffectDefinition effect), $"Project effect exists: {effectId}");
                    validation.Check(
                        effect != null && RequestEffectRuntime.TryGetContract(effectId, out string icon, out string script) &&
                        effect.EffectIconKey == icon && effect.EffectScriptKey == script,
                        $"Project effect contract matches: {effectId}");
                }
            }

            OutgameRequestTableLoadResult crlf = OutgameRequestCatalog.LoadFromCsv(
                projectRequests.Replace("\n", "\r\n"),
                projectEffects.Replace("\n", "\r\n"));
            validation.Check(crlf.Success, "CRLF supported");
            validation.Check(OutgameRequestCatalog.LoadFromCsv("\uFEFF" + projectRequests, projectEffects).Success, "UTF-8 BOM supported");

            string disabledRequests = Requests(
                RequestRow("DISABLED", "FALSE", "Normal", "Easy", "11"),
                RequestRow("ENABLED", "TRUE", "Sudden", "Hard", "12"));
            OutgameRequestTableLoadResult disabled = OutgameRequestCatalog.LoadFromCsv(disabledRequests, ValidEffects());
            validation.Check(disabled.Success, "Disabled request parses successfully");
            validation.Check(disabled.Catalog != null && disabled.Catalog.Requests.Count == 2, "Disabled request remains in catalog");
            validation.Check(disabled.Catalog != null && disabled.Catalog.EnabledRequests.Count == 1, "Enabled request filter available");
            validation.Check(disabled.Catalog != null && !disabled.Catalog.Requests[0].Enabled, "Disabled flag preserved");

            string effects = ValidEffects();
            ExpectFailure("Duplicate RequestId", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1"), RequestRow("A", "TRUE", "Sudden", "Hard", "2")), effects, "RequestId must be unique", validation);
            ExpectFailure("Duplicate EffectId", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1")), Effects(EffectRow("FX1", "TRUE"), EffectRow("FX1", "TRUE")), "EffectId must be unique", validation);
            ExpectFailure("Duplicate PermanentSeed", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1"), RequestRow("B", "TRUE", "Sudden", "Hard", "1")), effects, "already owned", validation);
            ExpectFailure("PermanentSeed zero", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "0")), effects, "greater than zero", validation);
            ExpectFailure("Invalid RequestType", Requests(RequestRow("A", "TRUE", "Other", "Easy", "1")), effects, "RequestType must", validation);
            ExpectFailure("Invalid Difficulty", Requests(RequestRow("A", "TRUE", "Normal", "Other", "1")), effects, "Difficulty must", validation);
            ExpectFailure("Effect gap", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", string.Empty, "FX1", string.Empty)), effects, "left-aligned", validation);
            ExpectFailure("Duplicate request EffectId", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", "FX1", "FX1", string.Empty)), effects, "more than once", validation);
            ExpectFailure("Missing EffectId", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", "MISSING", string.Empty, string.Empty)), effects, "does not exist", validation);
            ExpectFailure("Disabled EffectId", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", "FX_OFF", string.Empty, string.Empty)), Effects(EffectRow("FX_OFF", "FALSE")), "disabled", validation);
            ExpectFailure("Unclosed quote", RequestHeader + "\nA,TRUE,Normal,Easy,1,이름,key,title,\"unclosed,,,", effects, "not closed", validation);
            ExpectFailure("Wrong header", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1")).Replace("RequestId", "WrongId"), effects, "Header at position", validation);
            ExpectFailure("Reordered header", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1")).Replace("RequestId,Enabled", "Enabled,RequestId"), effects, "Header at position", validation);
            ExpectFailure("No enabled request", Requests(RequestRow("A", "FALSE", "Normal", "Easy", "1")), effects, "At least one enabled", validation);
            ExpectFailure("Invalid Enabled", Requests(RequestRow("A", "yes", "Normal", "Easy", "1")), effects, "exactly TRUE or FALSE", validation);
            ExpectFailure("Missing required string", Requests(RequestRow("A", "TRUE", "Normal", "Easy", "1", string.Empty, string.Empty, string.Empty, " ")), effects, "Required value", validation);
            return validation;
        }

        private static void ValidateLoadedCatalog(
            string label,
            OutgameRequestTableLoadResult result,
            ValidationState validation)
        {
            validation.Check(result.Success, label + " actual StreamingAssets load succeeds");
            validation.Check(result.Errors.Count == 0, label + " load has no errors");
            validation.Check(result.Catalog != null, label + " catalog exists");
            if (result.Catalog == null) return;
            OutgameRequestCatalog catalog = result.Catalog;
            validation.Check(catalog.Requests.Count > 0, label + " request catalog is not empty");
            validation.Check(catalog.Effects.Count == RequestEffectRuntime.SupportedEffectCount, label + " effect count");
            validation.Check(catalog.EnabledRequests.Count > 0, label + " enabled request catalog is not empty");
            validation.Check(catalog.Requests.All(value => value.Effects.Count <= 3), label + " effect count is at most three");
        }

        private static void ExpectFailure(
            string label,
            string requests,
            string effects,
            string reasonFragment,
            ValidationState validation)
        {
            OutgameRequestTableLoadResult result = OutgameRequestCatalog.LoadFromCsv(requests, effects);
            validation.Check(!result.Success, label + " fails");
            validation.Check(result.Catalog == null, label + " returns no partial catalog");
            validation.Check(result.Errors.Count > 0, label + " returns errors");
            validation.Check(
                result.Errors.Any(error => error.Reason.IndexOf(reasonFragment, StringComparison.OrdinalIgnoreCase) >= 0),
                label + " reports expected reason");
        }

        private static string Requests(params string[] rows)
        {
            return RequestHeader + "\n" + string.Join("\n", rows);
        }

        private static string Effects(params string[] rows)
        {
            return EffectHeader + "\n" + string.Join("\n", rows);
        }

        private static string RequestRow(
            string id,
            string enabled,
            string type,
            string difficulty,
            string seed,
            string effect1 = "",
            string effect2 = "",
            string effect3 = "",
            string requesterName = "임시 의뢰주")
        {
            return string.Join(",", new[]
            {
                id, enabled, type, difficulty, seed, "101", "301", "Img_bigtiles1", requesterName, "portrait", "임시 제목",
                "임시 설명\\n두 번째 줄", effect1, effect2, effect3
            });
        }

        private static string EffectRow(string id, string enabled, string icon = "icon", string script = "script")
        {
            return string.Join(",", new[] { id, enabled, "임시 효과", icon, script, "임시 효과 설명" });
        }

        private static string ValidEffects()
        {
            string[] ids = { RequestEffectRuntime.Hard, RequestEffectRuntime.Slippery, RequestEffectRuntime.NoItem,
                RequestEffectRuntime.Fatigue, RequestEffectRuntime.Super, RequestEffectRuntime.TimeDown, RequestEffectRuntime.Chemical };
            return Effects(ids.Select(id =>
            {
                RequestEffectRuntime.TryGetContract(id, out string icon, out string script);
                return EffectRow(id, "TRUE", icon, script);
            }).ToArray());
        }

        private static string ReadProjectRequests()
        {
            return File.ReadAllText(
                Path.Combine(Application.streamingAssetsPath, "Data", OutgameRequestTableLoader.RequestsFileName),
                Encoding.UTF8);
        }

        private static string ReadProjectEffects()
        {
            return File.ReadAllText(
                Path.Combine(Application.streamingAssetsPath, "Data", OutgameRequestTableLoader.EffectsFileName),
                Encoding.UTF8);
        }

        private static string SerializeFailures(IReadOnlyList<string> failures)
        {
            return string.Join(
                "\u001E",
                failures.Select(value => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))));
        }

        private static IReadOnlyList<string> DeserializeFailures(string value)
        {
            if (string.IsNullOrEmpty(value)) return Array.Empty<string>();
            var failures = new List<string>();
            string[] encoded = value.Split(new[] { '\u001E' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < encoded.Length; i++)
            {
                try { failures.Add(Encoding.UTF8.GetString(Convert.FromBase64String(encoded[i]))); }
                catch (FormatException) { failures.Add(encoded[i]); }
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
            SessionState.EraseString(ScenePathKey);
            SessionState.EraseBool(SceneDirtyKey);
        }

        private static void LogResult(string mode, ValidationState validation)
        {
            string message = $"[Outgame][Stage1][{mode}] result={validation.Passed}/{validation.Total}, failures={validation.Failures.Count}";
            if (validation.Failures.Count == 0)
            {
                Debug.Log(message);
                return;
            }
            Debug.LogError(message + "\n" + string.Join("\n", validation.Failures));
        }

        private sealed class ValidationState
        {
            private readonly List<string> failures;

            public ValidationState()
                : this(0, 0, null)
            {
            }

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
        }
    }
}
