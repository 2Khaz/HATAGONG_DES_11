using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using HATAGONG.GameFlow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HATAGONG.Outgame.Editor
{
    public static class OutgameRequestStage2Validation
    {
        public const int ExpectedEditModeAssertions = 330;
        public const int ExpectedPlayModeAssertions = 622;

        private const int FixedCandidateSeed = 20260716;
        private const string PlayRequestedKey = "HATAGONG.Outgame.Stage2.PlayRequested";
        private const string PlayRunningKey = "HATAGONG.Outgame.Stage2.PlayRunning";
        private const string PlayAwaitingExitKey = "HATAGONG.Outgame.Stage2.PlayAwaitingExit";
        private const string PlayPassedKey = "HATAGONG.Outgame.Stage2.PlayPassed";
        private const string PlayTotalKey = "HATAGONG.Outgame.Stage2.PlayTotal";
        private const string PlayFailuresKey = "HATAGONG.Outgame.Stage2.PlayFailures";
        private const string ScenePathKey = "HATAGONG.Outgame.Stage2.ScenePath";
        private const string SceneDirtyKey = "HATAGONG.Outgame.Stage2.SceneDirty";
        private const string SceneRootCountKey = "HATAGONG.Outgame.Stage2.SceneRootCount";
        private const string IngameHashKey = "HATAGONG.Outgame.Stage2.IngameHash";

        [InitializeOnLoadMethod]
        private static void RegisterPlayModeCallback()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 2 EditMode")]
        public static void ValidateEditMode()
        {
            var validation = new ValidationState();
            try
            {
                OutgameRequestTableLoadResult loaded = LoadProjectCatalog();
                validation.Check(loaded.Success, "Actual project CSV succeeds");
                validation.Check(loaded.Catalog != null, "Actual project catalog exists");
                validation.Check(loaded.Errors.Count == 0, "Actual project CSV has no errors");
                if (loaded.Catalog != null) RunEditModeContracts(loaded.Catalog, validation);
            }
            catch (Exception exception)
            {
                validation.Fail("Unexpected EditMode exception: " + exception);
            }
            LogResult("EditMode", validation, ExpectedEditModeAssertions);
        }

        [MenuItem("Tools/HATAGONG/Outgame/Validate Stage 2 PlayMode")]
        public static void ValidatePlayMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Outgame][Stage2][PlayMode] A Play Mode transition is already active.");
                return;
            }

            Scene scene = EditorSceneManager.GetActiveScene();
            SessionState.SetBool(PlayRequestedKey, true);
            SessionState.SetBool(PlayRunningKey, false);
            SessionState.SetBool(PlayAwaitingExitKey, false);
            SessionState.SetString(ScenePathKey, scene.path ?? string.Empty);
            SessionState.SetBool(SceneDirtyKey, scene.isDirty);
            SessionState.SetInt(SceneRootCountKey, scene.rootCount);
            SessionState.SetString(IngameHashKey, ComputeAssetHash("Assets/Scenes/INGAME.unity"));
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
                "OUTGAME active Scene path remains unchanged");
            validation.Check(
                scene.isDirty == SessionState.GetBool(SceneDirtyKey, false),
                "OUTGAME Scene dirty state remains unchanged");
            validation.Check(
                scene.rootCount == SessionState.GetInt(SceneRootCountKey, -1),
                "OUTGAME Scene root count remains unchanged");
            validation.Check(
                string.Equals(
                    ComputeAssetHash("Assets/Scenes/INGAME.unity"),
                    SessionState.GetString(IngameHashKey, string.Empty),
                    StringComparison.Ordinal),
                "INGAME Scene asset remains unchanged");

            ClearPlaySession();
            LogResult("PlayMode", validation, ExpectedPlayModeAssertions);
        }

        private static void RunEditModeContracts(
            OutgameRequestCatalog catalog,
            ValidationState validation)
        {
            string catalogBefore = CatalogSignature(catalog);
            int[] permanentBefore = catalog.Requests.Select(value => value.PermanentSeed).ToArray();

            OutgameRequestOfferGenerationResult baseline =
                OutgameRequestOfferGenerator.Generate(catalog, FixedCandidateSeed);
            validation.Check(baseline.Success, "Determinism baseline succeeds");
            validation.Check(baseline.Batch != null, "Determinism baseline batch exists");
            string baselineSignature = BatchSignature(baseline.Batch);
            for (int i = 0; i < 10; i++)
            {
                OutgameRequestOfferGenerationResult repeated =
                    OutgameRequestOfferGenerator.Generate(catalog, FixedCandidateSeed);
                validation.Check(repeated.Success, $"Determinism repeat {i} succeeds");
                validation.Check(repeated.Batch != null, $"Determinism repeat {i} batch exists");
                validation.Check(repeated.Batch != null && repeated.Batch.BatchSeed == FixedCandidateSeed,
                    $"Determinism repeat {i} preserves BatchSeed");
                validation.Check(BatchSignature(repeated.Batch) == baselineSignature,
                    $"Determinism repeat {i} matches IDs/order/seeds");
            }

            for (int count = 4; count >= 1; count--)
            {
                OutgameRequestOfferGenerationResult sized =
                    OutgameRequestOfferGenerator.Generate(CreateCatalog(count), 17);
                validation.Check(sized.Success, $"Enabled count {count} succeeds");
                validation.Check(sized.Batch != null, $"Enabled count {count} batch exists");
                validation.Check(sized.Batch != null && sized.Batch.Offers.Count == 3,
                    $"Enabled count {count} returns required candidate count");
            }

            OutgameRequestOfferGenerationResult twoEnabled =
                OutgameRequestOfferGenerator.Generate(CreateCatalog(2), 17);
            validation.Check(twoEnabled.Batch != null && twoEnabled.Batch.Offers.Count == 3,
                "Two enabled requests return three offers");
            validation.Check(twoEnabled.Batch != null && twoEnabled.Batch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == 2,
                "Two enabled requests keep both RequestIds");
            validation.Check(twoEnabled.Batch != null &&
                twoEnabled.Batch.Offers.Any(value => value.Definition.RequestId == "FIXTURE_0") &&
                twoEnabled.Batch.Offers.Any(value => value.Definition.RequestId == "FIXTURE_1"),
                "Both two-request Fixture definitions are included");
            validation.Check(twoEnabled.Batch != null && twoEnabled.Batch.Offers.GroupBy(value => value.Definition.RequestId).Count(group => group.Count() == 2) == 1,
                "Exactly one of two enabled requests is duplicated");
            validation.Check(twoEnabled.Batch != null && DuplicateOffersShareDefinition(twoEnabled.Batch),
                "Duplicate offers share the same Definition");
            validation.Check(twoEnabled.Batch != null && DuplicateOffersShareContract(twoEnabled.Batch),
                "Duplicate offers share Difficulty, type, PermanentSeed and Phase Seeds");

            OutgameRequestOfferGenerationResult oneEnabled =
                OutgameRequestOfferGenerator.Generate(CreateCatalog(1), 17);
            validation.Check(oneEnabled.Batch != null && oneEnabled.Batch.Offers.Count == 3,
                "One enabled request returns three offers");
            validation.Check(oneEnabled.Batch != null && oneEnabled.Batch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == 1,
                "One enabled request repeats one RequestId");
            validation.Check(oneEnabled.Batch != null && oneEnabled.Batch.Offers.All(value => ReferenceEquals(value.Definition, oneEnabled.Batch.Offers[0].Definition)),
                "One enabled request repeats the same Definition");
            validation.Check(oneEnabled.Batch != null && DuplicateOffersShareContract(oneEnabled.Batch),
                "One enabled request repeats the same Difficulty, type, PermanentSeed and Phase Seeds");

            var duplicatedIds = new HashSet<string>(StringComparer.Ordinal);
            OutgameRequestCatalog twoCatalog = CreateCatalog(2);
            for (int seed = 0; seed < 64; seed++)
            {
                OutgameRequestOfferBatch batch = OutgameRequestOfferGenerator.Generate(twoCatalog, seed).Batch;
                if (batch == null) continue;
                foreach (IGrouping<string, OutgameRequestOffer> group in batch.Offers.GroupBy(value => value.Definition.RequestId))
                    if (group.Count() == 2) duplicatedIds.Add(group.Key);
            }
            validation.Check(duplicatedIds.Count == 2,
                "Across BatchSeeds either of two enabled requests can be duplicated");
            AssertFailure(
                OutgameRequestOfferGenerator.Generate(CreateCatalog(0), 17),
                "Zero enabled requests",
                validation);

            validation.Check(
                baseline.Batch != null && baseline.Batch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == baseline.Batch.Offers.Count,
                "Offer RequestIds are unique");
            validation.Check(CatalogSignature(catalog) == catalogBefore, "Catalog is unchanged after generation");
            validation.Check(OffersAreReadOnly(baseline.Batch), "Batch offers collection is read-only");

            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            for (int seed = 0; seed < 64; seed++)
            {
                OutgameRequestOfferGenerationResult generated =
                    OutgameRequestOfferGenerator.Generate(catalog, seed);
                validation.Check(generated.Success, $"Diversity seed {seed} succeeds");
                validation.Check(generated.Batch != null && generated.Batch.Offers.Count == 3,
                    $"Diversity seed {seed} returns 3");
                validation.Check(generated.Batch != null && generated.Batch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == 3,
                    $"Diversity seed {seed} has unique IDs");
                if (generated.Batch == null) continue;
                signatures.Add(RequestIdSignature(generated.Batch));
                foreach (OutgameRequestOffer offer in generated.Batch.Offers)
                    seenIds.Add(offer.Definition.RequestId);
            }
            validation.Check(signatures.Count >= 2, "64 BatchSeeds produce at least two signatures");
            validation.Check(seenIds.Count == catalog.EnabledRequests.Count, "64 BatchSeeds expose every enabled request");
            validation.Check(
                catalog.Requests.Select(value => value.PermanentSeed).SequenceEqual(permanentBefore),
                "PermanentSeeds remain unchanged after diversity generation");

            for (int definitionIndex = 0; definitionIndex < catalog.Requests.Count; definitionIndex++)
            {
                OutgameRequestDefinition definition = catalog.Requests[definitionIndex];
                OutgameRequestOffer first = FindOffer(catalog, definition.RequestId, 0, 512, -1);
                int firstBatchSeed = FindBatchSeed(catalog, definition.RequestId, 0, 512, -1);
                int firstIndex = FindOfferIndex(catalog, definition.RequestId, firstBatchSeed);
                OutgameRequestOffer second = FindOfferAtDifferentIndex(
                    catalog,
                    definition.RequestId,
                    512,
                    2048,
                    firstIndex);
                validation.Check(first != null, definition.RequestId + " appears in a batch");
                validation.Check(first != null && first.Phase1Seed > 0 && first.Phase3Seed > 0,
                    definition.RequestId + " phase seeds are positive and nonzero");
                validation.Check(first != null && first.Phase1Seed == definition.Phase1Seed && first.Phase3Seed == definition.Phase3Seed,
                    definition.RequestId + " phase seeds come directly from CSV definition");
                validation.Check(first != null && ReferenceEquals(first.Definition, definition),
                    definition.RequestId + " offer owns the original definition reference");
                validation.Check(definition.PermanentSeed == permanentBefore[definitionIndex],
                    definition.RequestId + " PermanentSeed is unchanged");
                validation.Check(second != null, definition.RequestId + " appears in another batch and index");
                validation.Check(first != null && second != null && PhaseSeedSignature(first) == PhaseSeedSignature(second),
                    definition.RequestId + " phase seeds ignore BatchSeed and index");
            }

            AssertFailure(OutgameRequestOfferGenerator.Generate(null, 1), "Null catalog", validation);
            AssertFailure(OutgameRequestOfferGenerator.Generate(CreateCatalog(0), 1), "Zero enabled catalog", validation);
            AssertFailure(OutgameRequestOfferGenerator.Generate(CreateNullRequestCatalog(), 1), "Null request", validation);
            AssertFailure(OutgameRequestOfferGenerator.Generate(CreateDuplicateIdCatalog(), 1), "Duplicate RequestId", validation);
            AssertFailure(OutgameRequestOfferGenerator.Generate(CreateDuplicateSeedCatalog(), 1), "Duplicate PermanentSeed", validation);
            AssertFailure(OutgameRequestOfferGenerator.Generate(CreateNonPositiveSeedCatalog(), 1), "Non-positive PermanentSeed", validation);
            AssertFailure(OutgameRequestOfferGenerator.Generate(CreateNegativeSeedCatalog(), 1), "Negative PermanentSeed", validation);

            foreach (int batchSeed in new[] { 0, -123456789 })
            {
                OutgameRequestOfferGenerationResult generated =
                    OutgameRequestOfferGenerator.Generate(catalog, batchSeed);
                validation.Check(generated.Success, $"BatchSeed {batchSeed} succeeds");
                validation.Check(generated.Batch != null && generated.Batch.BatchSeed == batchSeed,
                    $"BatchSeed {batchSeed} is preserved");
                validation.Check(generated.Batch != null && generated.Batch.Offers.Count == 3,
                    $"BatchSeed {batchSeed} returns 3");
            }

            validation.Check(typeof(OutgameRequestOffer).GetProperty(nameof(OutgameRequestOffer.Definition)).SetMethod == null,
                "Offer Definition is get-only");
            validation.Check(typeof(OutgameRequestOffer).GetProperty(nameof(OutgameRequestOffer.Phase1Seed)).SetMethod == null,
                "Offer Phase1Seed is get-only");
            validation.Check(typeof(OutgameRequestOffer).GetProperty(nameof(OutgameRequestOffer.Phase3ImageKey)).SetMethod == null,
                "Offer Phase3ImageKey is get-only");
            validation.Check(typeof(OutgameRequestOffer).GetProperty("Phase2Seed") == null,
                "Offer does not expose a Phase2Seed");
            validation.Check(typeof(OutgameRequestOffer).GetProperty(nameof(OutgameRequestOffer.Phase3Seed)).SetMethod == null,
                "Offer Phase3Seed is get-only");
            validation.Check(typeof(OutgameRequestOfferBatch).GetProperty(nameof(OutgameRequestOfferBatch.BatchSeed)).SetMethod == null,
                "Batch BatchSeed is get-only");
            validation.Check(typeof(OutgameRequestOfferBatch).GetProperty(nameof(OutgameRequestOfferBatch.Offers)).SetMethod == null,
                "Batch Offers is get-only");
        }

        private static async void RunPlayModeValidationAsync()
        {
            var validation = new ValidationState();
            try
            {
                OutgameRequestTableLoadResult loaded = await OutgameRequestTableLoader.LoadAsync();
                validation.Check(loaded.Success, "Actual StreamingAssets load succeeds");
                validation.Check(loaded.Errors.Count == 0, "Actual StreamingAssets load has no errors");
                validation.Check(loaded.Catalog != null, "Actual StreamingAssets catalog exists");
                if (loaded.Catalog != null) RunPlayModeContracts(loaded.Catalog, validation);
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

        private static void RunPlayModeContracts(
            OutgameRequestCatalog catalog,
            ValidationState validation)
        {
            string catalogBefore = CatalogSignature(catalog);
            OutgameRequestOfferGenerationResult fixedResult =
                OutgameRequestOfferGenerator.Generate(catalog, FixedCandidateSeed);
            validation.Check(fixedResult.Success, "Scenario A fixed BatchSeed succeeds");
            validation.Check(fixedResult.Batch != null, "Scenario A batch exists");
            validation.Check(fixedResult.Batch != null && fixedResult.Batch.Offers.Count == 3, "Scenario A returns 3");
            validation.Check(fixedResult.Batch != null && fixedResult.Batch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == 3,
                "Scenario A IDs are unique");
            validation.Check(fixedResult.Batch != null && fixedResult.Batch.Offers.All(value => value.Definition.Enabled),
                "Scenario A definitions are enabled");
            validation.Check(fixedResult.Batch != null && fixedResult.Batch.Offers.All(value => value.Phase1Seed > 0 && value.Phase3Seed > 0),
                "Scenario A phase seeds are positive");
            validation.Check(fixedResult.Batch != null && fixedResult.Batch.Offers.All(value => value.Phase1Seed == value.Definition.Phase1Seed && value.Phase3Seed == value.Definition.Phase3Seed),
                "Scenario A phase seeds are exact definition values");
            validation.Check(fixedResult.Batch != null && fixedResult.Batch.Offers.All(value => catalog.Requests.Any(definition => ReferenceEquals(definition, value.Definition))),
                "Scenario A definitions come from actual catalog");

            string fixedSignature = BatchSignature(fixedResult.Batch);
            for (int i = 0; i < 20; i++)
            {
                OutgameRequestOfferGenerationResult repeated =
                    OutgameRequestOfferGenerator.Generate(catalog, FixedCandidateSeed);
                validation.Check(repeated.Success, $"Scenario B repeat {i} succeeds");
                validation.Check(repeated.Batch != null, $"Scenario B repeat {i} batch exists");
                validation.Check(BatchSignature(repeated.Batch) == fixedSignature,
                    $"Scenario B repeat {i} matches IDs/order/seeds");
                validation.Check(CatalogSignature(catalog) == catalogBefore,
                    $"Scenario B repeat {i} leaves catalog unchanged");
            }

            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var phaseSeeds = new Dictionary<string, string>(StringComparer.Ordinal);
            var positions = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            bool stableSeeds = true;
            for (int seed = 0; seed < 128; seed++)
            {
                OutgameRequestOfferGenerationResult generated =
                    OutgameRequestOfferGenerator.Generate(catalog, seed);
                validation.Check(generated.Success, $"Scenario C seed {seed} succeeds");
                validation.Check(generated.Batch != null, $"Scenario C seed {seed} batch exists");
                validation.Check(generated.Batch != null && generated.Batch.Offers.Count == 3,
                    $"Scenario C seed {seed} returns 3");
                validation.Check(generated.Batch != null && generated.Batch.Offers.Select(value => value.Definition.RequestId).Distinct().Count() == 3,
                    $"Scenario C seed {seed} has unique IDs");
                if (generated.Batch == null) continue;
                signatures.Add(RequestIdSignature(generated.Batch));
                for (int offerIndex = 0; offerIndex < generated.Batch.Offers.Count; offerIndex++)
                {
                    OutgameRequestOffer offer = generated.Batch.Offers[offerIndex];
                    seenIds.Add(offer.Definition.RequestId);
                    if (!positions.TryGetValue(offer.Definition.RequestId, out HashSet<int> requestPositions))
                    {
                        requestPositions = new HashSet<int>();
                        positions.Add(offer.Definition.RequestId, requestPositions);
                    }
                    requestPositions.Add(offerIndex);
                    string current = PhaseSeedSignature(offer);
                    if (phaseSeeds.TryGetValue(offer.Definition.RequestId, out string previous))
                        stableSeeds &= previous == current;
                    else
                        phaseSeeds.Add(offer.Definition.RequestId, current);
                }
            }
            validation.Check(signatures.Count >= 2, "Scenario C has at least two signatures");
            validation.Check(seenIds.Count == catalog.EnabledRequests.Count, "Scenario C exposes all four requests");
            validation.Check(stableSeeds, "Scenario C phase seeds remain stable per request");
            validation.Check(CatalogSignature(catalog) == catalogBefore, "Scenario C leaves catalog unchanged");

            foreach (int count in new[] { 2, 1 })
            {
                OutgameRequestOfferGenerationResult sized =
                    OutgameRequestOfferGenerator.Generate(CreateCatalog(count), 88);
                validation.Check(sized.Success, $"Scenario D enabled count {count} succeeds");
                validation.Check(sized.Batch != null, $"Scenario D enabled count {count} batch exists");
                validation.Check(sized.Batch != null && sized.Batch.Offers.Count == 3,
                    $"Scenario D enabled count {count} returns 3");
            }
            AssertFailure(
                OutgameRequestOfferGenerator.Generate(CreateCatalog(0), 88),
                "Scenario D zero enabled",
                validation);

            validation.Check(
                phaseSeeds.Count == catalog.Requests.Count &&
                stableSeeds &&
                positions.Count == catalog.Requests.Count &&
                positions.Values.All(value => value.Count > 1),
                "Scenario E all definitions retain phase seeds across different batches and indices");
            validation.Check(CatalogSignature(catalog) == catalogBefore,
                "Scenario E PermanentSeeds and catalog remain unchanged");
        }

        private static OutgameRequestTableLoadResult LoadProjectCatalog()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "Data");
            return OutgameRequestCatalog.LoadFromCsv(
                File.ReadAllText(Path.Combine(root, OutgameRequestTableLoader.RequestsFileName), Encoding.UTF8),
                File.ReadAllText(Path.Combine(root, OutgameRequestTableLoader.EffectsFileName), Encoding.UTF8));
        }

        private static OutgameRequestCatalog CreateCatalog(int enabledCount)
        {
            var requests = new List<OutgameRequestDefinition>();
            int count = Math.Max(1, enabledCount);
            for (int i = 0; i < count; i++)
                requests.Add(CreateDefinition("FIXTURE_" + i, i < enabledCount, 700001 + i));
            return InvokeCatalogConstructor(requests);
        }

        private static OutgameRequestCatalog CreateNullRequestCatalog()
        {
            OutgameRequestCatalog catalog = CreateCatalog(1);
            SetCatalogCollections(catalog, new OutgameRequestDefinition[] { null });
            return catalog;
        }

        private static OutgameRequestCatalog CreateDuplicateIdCatalog()
        {
            OutgameRequestCatalog catalog = CreateCatalog(2);
            SetCatalogCollections(catalog, new[]
            {
                CreateDefinition("DUPLICATE", true, 710001),
                CreateDefinition("DUPLICATE", true, 710002)
            });
            return catalog;
        }

        private static OutgameRequestCatalog CreateDuplicateSeedCatalog()
        {
            return InvokeCatalogConstructor(new[]
            {
                CreateDefinition("SEED_A", true, 720001),
                CreateDefinition("SEED_B", true, 720001)
            });
        }

        private static OutgameRequestCatalog CreateNonPositiveSeedCatalog()
        {
            return InvokeCatalogConstructor(new[] { CreateDefinition("BAD_SEED", true, 0) });
        }

        private static OutgameRequestCatalog CreateNegativeSeedCatalog()
        {
            return InvokeCatalogConstructor(new[] { CreateDefinition("NEGATIVE_SEED", true, -1) });
        }

        private static OutgameRequestDefinition CreateDefinition(
            string requestId,
            bool enabled,
            int permanentSeed)
        {
            return new OutgameRequestDefinition(
                requestId,
                enabled,
                RequestType.Normal,
                GameDifficulty.Normal,
                permanentSeed,
                101001,
                103001,
                "Img_bigtiles1",
                "Fixture",
                "fixture_portrait",
                "Fixture Title",
                "Fixture Description",
                Array.Empty<OutgameRequestEffectDefinition>());
        }

        private static OutgameRequestCatalog InvokeCatalogConstructor(
            IEnumerable<OutgameRequestDefinition> requests)
        {
            ConstructorInfo constructor = typeof(OutgameRequestCatalog).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(IEnumerable<OutgameRequestDefinition>), typeof(IEnumerable<OutgameRequestEffectDefinition>) },
                null);
            if (constructor == null) throw new InvalidOperationException("Catalog constructor was not found.");
            return (OutgameRequestCatalog)constructor.Invoke(new object[]
            {
                requests,
                Array.Empty<OutgameRequestEffectDefinition>()
            });
        }

        private static void SetCatalogCollections(
            OutgameRequestCatalog catalog,
            IEnumerable<OutgameRequestDefinition> requests)
        {
            OutgameRequestDefinition[] values = requests.ToArray();
            var readOnly = new ReadOnlyCollection<OutgameRequestDefinition>(values);
            SetField(catalog, "requests", readOnly);
            SetField(catalog, "enabledRequests", readOnly);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Catalog field was not found: " + fieldName);
            field.SetValue(target, value);
        }

        private static void AssertFailure(
            OutgameRequestOfferGenerationResult result,
            string label,
            ValidationState validation)
        {
            validation.Check(!result.Success, label + " fails explicitly");
            validation.Check(result.Batch == null, label + " returns no partial batch");
            validation.Check(result.Errors.Count > 0, label + " returns errors");
        }

        private static bool DuplicateOffersShareDefinition(OutgameRequestOfferBatch batch)
        {
            foreach (IGrouping<string, OutgameRequestOffer> group in batch.Offers.GroupBy(value => value.Definition.RequestId))
            {
                OutgameRequestOffer[] offers = group.ToArray();
                if (offers.Length < 2) continue;
                if (offers.Any(value => !ReferenceEquals(value.Definition, offers[0].Definition))) return false;
            }
            return true;
        }

        private static bool DuplicateOffersShareContract(OutgameRequestOfferBatch batch)
        {
            foreach (IGrouping<string, OutgameRequestOffer> group in batch.Offers.GroupBy(value => value.Definition.RequestId))
            {
                OutgameRequestOffer[] offers = group.ToArray();
                if (offers.Length < 2) continue;
                OutgameRequestOffer first = offers[0];
                if (offers.Any(value =>
                    value.Definition.Difficulty != first.Definition.Difficulty ||
                    value.Definition.RequestType != first.Definition.RequestType ||
                    value.Definition.PermanentSeed != first.Definition.PermanentSeed ||
                    value.Phase1Seed != first.Phase1Seed ||
                    value.Phase3Seed != first.Phase3Seed ||
                    value.Phase3ImageKey != first.Phase3ImageKey)) return false;
            }
            return true;
        }

        private static bool OffersAreReadOnly(OutgameRequestOfferBatch batch)
        {
            if (batch == null) return false;
            if (!(batch.Offers is IList<OutgameRequestOffer> list)) return true;
            try
            {
                list.Add(batch.Offers[0]);
                return false;
            }
            catch (NotSupportedException)
            {
                return true;
            }
        }

        private static OutgameRequestOffer FindOffer(
            OutgameRequestCatalog catalog,
            string requestId,
            int startSeed,
            int endSeed,
            int excludedSeed)
        {
            for (int seed = startSeed; seed < endSeed; seed++)
            {
                if (seed == excludedSeed) continue;
                OutgameRequestOfferGenerationResult result = OutgameRequestOfferGenerator.Generate(catalog, seed);
                if (result.Batch == null) continue;
                OutgameRequestOffer offer = result.Batch.Offers.FirstOrDefault(
                    value => string.Equals(value.Definition.RequestId, requestId, StringComparison.Ordinal));
                if (offer != null) return offer;
            }
            return null;
        }

        private static int FindBatchSeed(
            OutgameRequestCatalog catalog,
            string requestId,
            int startSeed,
            int endSeed,
            int excludedSeed)
        {
            for (int seed = startSeed; seed < endSeed; seed++)
            {
                if (seed == excludedSeed) continue;
                OutgameRequestOfferGenerationResult result = OutgameRequestOfferGenerator.Generate(catalog, seed);
                if (result.Batch != null && result.Batch.Offers.Any(
                    value => string.Equals(value.Definition.RequestId, requestId, StringComparison.Ordinal)))
                    return seed;
            }
            return int.MinValue;
        }

        private static int FindOfferIndex(
            OutgameRequestCatalog catalog,
            string requestId,
            int batchSeed)
        {
            OutgameRequestOfferGenerationResult result =
                OutgameRequestOfferGenerator.Generate(catalog, batchSeed);
            if (result.Batch == null) return -1;
            for (int i = 0; i < result.Batch.Offers.Count; i++)
            {
                if (string.Equals(result.Batch.Offers[i].Definition.RequestId, requestId, StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        private static OutgameRequestOffer FindOfferAtDifferentIndex(
            OutgameRequestCatalog catalog,
            string requestId,
            int startSeed,
            int endSeed,
            int excludedIndex)
        {
            for (int seed = startSeed; seed < endSeed; seed++)
            {
                OutgameRequestOfferGenerationResult result =
                    OutgameRequestOfferGenerator.Generate(catalog, seed);
                if (result.Batch == null) continue;
                for (int i = 0; i < result.Batch.Offers.Count; i++)
                {
                    OutgameRequestOffer offer = result.Batch.Offers[i];
                    if (i != excludedIndex &&
                        string.Equals(offer.Definition.RequestId, requestId, StringComparison.Ordinal))
                        return offer;
                }
            }
            return null;
        }

        private static string CatalogSignature(OutgameRequestCatalog catalog)
        {
            return string.Join("|", catalog.Requests.Select(value => value == null
                ? "<null>"
                : $"{value.RequestId}:{value.Enabled}:{value.PermanentSeed}"));
        }

        private static string BatchSignature(OutgameRequestOfferBatch batch)
        {
            if (batch == null) return "<null>";
            return batch.BatchSeed + "|" + string.Join("|", batch.Offers.Select(value =>
                $"{value.Definition.RequestId}:{value.Phase1Seed}:{value.Phase3Seed}:{value.Phase3ImageKey}"));
        }

        private static string RequestIdSignature(OutgameRequestOfferBatch batch)
        {
            return string.Join("|", batch.Offers.Select(value => value.Definition.RequestId));
        }

        private static string PhaseSeedSignature(OutgameRequestOffer offer)
        {
            return $"{offer.Phase1Seed}:{offer.Phase3Seed}:{offer.Phase3ImageKey}";
        }

        private static string ComputeAssetHash(string projectRelativePath)
        {
            string path = Path.GetFullPath(projectRelativePath);
            if (!File.Exists(path)) return "<missing>";
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static string SerializeFailures(IReadOnlyList<string> failures)
        {
            return string.Join("\u001E", failures.Select(value =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(value))));
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
            SessionState.EraseString(ScenePathKey);
            SessionState.EraseBool(SceneDirtyKey);
            SessionState.EraseInt(SceneRootCountKey);
            SessionState.EraseString(IngameHashKey);
        }

        private static void LogResult(string mode, ValidationState validation, int expected)
        {
            if (validation.Total != expected)
                validation.Fail($"Assertion total mismatch: expected={expected}, actual={validation.Total}");
            string message = $"[Outgame][Stage2][{mode}] result={validation.Passed}/{validation.Total}, expected={expected}, failures={validation.Failures.Count}";
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

            public void Fail(string name)
            {
                failures.Add(name);
            }
        }
    }
}
