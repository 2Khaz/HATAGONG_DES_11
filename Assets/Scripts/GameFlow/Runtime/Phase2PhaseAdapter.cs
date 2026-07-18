using System;
using System.Collections.Generic;
using HATAGONG.Phase2;
using HATAGONG.Phase2.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace HATAGONG.GameFlow
{
    public sealed class Phase2PhaseAdapter : MonoBehaviour, IGamePhase, IGameplayInputStatus
    {
        public const float CompletionDurationSeconds = 0.4f;
        [SerializeField] private Phase2MaskPresenter maskPresenter;
        [SerializeField] private Phase2PointerInputController pointerInput;
        [SerializeField] private GameScoreController scoreController;
        [SerializeField] private Image deckPanel;
        [SerializeField] private Sprite deckSprite;

        private Phase2PaintOrchestrator _orchestrator;
        private float _brushRadiusRatio;
        private float _baseBrushRadiusRatio;
        private readonly List<int> _unpaintedCellIndices = new List<int>(16384);
        private bool _lastVisualRefreshCalled;
        private bool _phaseClearedRaised;
        private bool _exitReadyRaised;
        private bool _completionStarted;
        private int _activeStrokeId;
        private int _strokeOverlayRemoved;
        private int _strokeChemicalBaseRemoved;
        private int _strokeSameStrokeBaseRemoved;
        private bool _chemicalEffectActive;
        private int _chemicalSeed;

        public GamePhaseId PhaseId => GamePhaseId.Phase2;
        public bool IsPrepared { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsExitReady { get; private set; }
        public bool InputEnabled => pointerInput && pointerInput.InputEnabled;
        public bool IsGameplayInputEnabled => InputEnabled && IsPrepared && IsRunning && !IsCleared && !IsExitReady;
        public string LastFailureReason { get; private set; } = string.Empty;
        public Phase2PaintSessionState CurrentState => _orchestrator?.CurrentState ?? Phase2PaintSessionState.Ready;
        public int PaintedCellCount => _orchestrator?.PaintedCellCount ?? 0;
        public int TotalCellCount => _orchestrator?.Config.TotalCellCount ?? 0;
        public int RequiredClearCells => _orchestrator?.Config.RequiredClearCells ?? 0;
        public double Progress01 => TotalCellCount > 0 ? (double)PaintedCellCount / TotalCellCount : 0d;
        public int PhaseScore => _orchestrator?.Score ?? 0;
        public float BrushRadiusRatio => _brushRadiusRatio;
        public RenderTexture MaskTexture => _orchestrator?.MaskTexture;
        public GraphicsFormat MaskFormat => _orchestrator?.MaskFormat ?? GraphicsFormat.None;
        public bool RendererInitialized => _orchestrator != null && _orchestrator.RendererInitialized;
        public int OwnedRenderTextureCount => _orchestrator?.OwnedRenderTextureCount ?? 0;
        public int RuntimeGenerationCount { get; private set; }
        public int ChemicalCellCount => _orchestrator?.ChemicalCellCount ?? 0;
        public GameRunContext RunContext { get; private set; }

        public event Action PhaseCleared;
        public event Action PhaseExitReady;

        public bool Prepare(GameRunContext context)
        {
            SetInputEnabled(false);
            pointerInput?.CancelActiveStroke();
            if (gameObject.activeSelf) gameObject.SetActive(false);
            LastFailureReason = string.Empty;

            if (!ApplyDeckSprite() || !context.IsValid || !maskPresenter || !pointerInput || !scoreController ||
                !TryGetPreset(context.Difficulty, out Phase2PaintConfig config, out float radius))
            {
                return FailPrepare("Invalid context or required Phase 2 scene reference.");
            }

            DisposeRuntime();
            try
            {
                int? chemicalSeed = context.HasEffect(RequestEffectRuntime.Chemical) ? context.PermanentSeed : (int?)null;
                _chemicalEffectActive = chemicalSeed.HasValue;
                _chemicalSeed = chemicalSeed ?? 0;
                _orchestrator = new Phase2PaintOrchestrator(config, chemicalSeed: chemicalSeed);
                if (!_orchestrator.Prepare())
                {
                    return FailPrepare("Phase 2 orchestrator preparation failed.");
                }
                _brushRadiusRatio = radius;
                _baseBrushRadiusRatio = radius;
                if (!maskPresenter.Bind(_orchestrator.MaskTexture))
                {
                    return FailPrepare("Phase 2 mask presenter binding failed.");
                }
                if (!maskPresenter.BindChemicalOverlay(_orchestrator.Grid, _chemicalSeed))
                {
                    return FailPrepare("Phase 2 chemical overlay binding failed.");
                }
                IsPrepared = true;
                IsRunning = false;
                IsCleared = false;
                IsExitReady = false;
                _phaseClearedRaised = false;
                _exitReadyRaised = false;
                _completionStarted = false;
                RuntimeGenerationCount++;
                RunContext = context;
                Debug.Log($"[RequestEffect][Phase2] chemical={_chemicalEffectActive}, totalCells={config.TotalCellCount}, chemicalCells={_orchestrator.ChemicalCellCount}, targetPercent={RequestEffectRuntime.ChemicalCellPercent}, permanentSeed={context.PermanentSeed}, overlayCreated={_orchestrator.ChemicalCellCount == 0 || _orchestrator.RemainingChemicalOverlayCount == _orchestrator.ChemicalCellCount}, sharedRemovalRules=Brush|Trowel|CementBasket", this);
                SetInputEnabled(false);
                return true;
            }
            catch (Exception exception)
            {
                return FailPrepare(exception.Message);
            }
        }

        public bool Activate()
        {
            SetInputEnabled(false);
            if (!ApplyDeckSprite() || !IsPrepared || _orchestrator == null || !maskPresenter || !pointerInput)
            {
                LastFailureReason = "Phase 2 is not prepared.";
                return false;
            }

            Phase2VisualRecoveryResult recovery = _orchestrator.RecoverVisualMaskIfNeeded();
            if (!recovery.Succeeded)
            {
                LastFailureReason = "Phase 2 visual recovery failed.";
                return false;
            }
            if (recovery.ShouldCancelActiveInput) pointerInput.CancelActiveStroke();

            gameObject.SetActive(true);
            if (!maskPresenter.Bind(_orchestrator.MaskTexture))
            {
                LastFailureReason = "Phase 2 mask presenter binding failed.";
                gameObject.SetActive(false);
                return false;
            }
            if (!maskPresenter.BindChemicalOverlay(_orchestrator.Grid, _chemicalSeed))
            {
                LastFailureReason = "Phase 2 chemical overlay binding failed.";
                gameObject.SetActive(false);
                return false;
            }
            if (_orchestrator.CurrentState == Phase2PaintSessionState.Ready && !_orchestrator.StartRunning())
            {
                LastFailureReason = "Phase 2 could not enter Running.";
                gameObject.SetActive(false);
                return false;
            }
            IsRunning = _orchestrator.CurrentState == Phase2PaintSessionState.Running;
            SetInputEnabled(false);
            return IsRunning;
        }

        public void Deactivate()
        {
            RestoreBaseBrushRadius();
            SetInputEnabled(false);
            pointerInput?.CancelActiveStroke();
            IsRunning = false;
            maskPresenter?.ReleaseBinding();
            gameObject.SetActive(false);
        }

        public void SetInputEnabled(bool enabled)
        {
            bool allowed = enabled && IsPrepared && IsRunning && !IsCleared && !IsExitReady;
            _orchestrator?.SetInputEnabled(allowed);
            if (pointerInput) pointerInput.SetInputEnabled(allowed);
        }

        public bool TryBeginStroke(Vector2 boardUv, out Phase2OrchestrationResult result)
        {
            if (!CanSubmit())
            {
                result = default;
                return false;
            }
            EndActiveStroke();
            _activeStrokeId = _orchestrator.CreateStrokeId();
            result = _orchestrator.RequestStamp(boardUv.x, boardUv.y, _brushRadiusRatio, true, _activeStrokeId);
            AccumulateChemicalStroke(result);
            return ConsumeResult(result);
        }

        public bool TryContinueStroke(Vector2 startUv, Vector2 endUv, out Phase2OrchestrationResult result)
        {
            if (!CanSubmit())
            {
                result = default;
                return false;
            }
            if (_activeStrokeId == 0)
            {
                result = default;
                return false;
            }
            result = _orchestrator.RequestSegment(startUv.x, startUv.y, endUv.x, endUv.y, _brushRadiusRatio, true, _activeStrokeId);
            AccumulateChemicalStroke(result);
            return ConsumeResult(result);
        }

        public void EndActiveStroke()
        {
            if (_activeStrokeId == 0) return;
            LogChemicalStroke(_activeStrokeId, _strokeOverlayRemoved, _strokeChemicalBaseRemoved, _strokeSameStrokeBaseRemoved);
            _activeStrokeId = 0;
            _strokeOverlayRemoved = 0;
            _strokeChemicalBaseRemoved = 0;
            _strokeSameStrokeBaseRemoved = 0;
        }

        public bool TryApplyStampBatch(IReadOnlyList<Phase2PaintStamp> stamps, out Phase2OrchestrationResult result)
        {
            if (!CanSubmit())
            {
                result = default;
                return false;
            }
            int strokeId = _orchestrator.CreateStrokeId();
            result = _orchestrator.RequestStampBatch(stamps, true, strokeId);
            bool consumed = ConsumeResult(result);
            LogChemicalStroke(strokeId, result.ChemicalOverlayRemovedCount, result.ChemicalBaseRemovedCount, result.SameStrokeChemicalBaseRemovedCount);
            return consumed;
        }

        public bool TrySetBrushRadiusMultiplier(float multiplier)
        {
            if (!IsPrepared || _baseBrushRadiusRatio <= 0f || multiplier <= 0f || float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return false;
            _brushRadiusRatio = _baseBrushRadiusRatio * multiplier;
            return true;
        }

        public void RestoreBaseBrushRadius()
        {
            if (_baseBrushRadiusRatio > 0f) _brushRadiusRatio = _baseBrushRadiusRatio;
        }

        public bool TryApplyRandomUnpaintedFraction(float fraction, out Phase2OrchestrationResult result)
        {
            result = default;
            if (!CanSubmit() || fraction < 0.08f || fraction > 0.12f) return false;
            _orchestrator.CollectUnpaintedCellIndices(_unpaintedCellIndices);
            if (_unpaintedCellIndices.Count == 0) return false;
            int targetCount = Mathf.Clamp(Mathf.CeilToInt(_unpaintedCellIndices.Count * fraction), 1, _unpaintedCellIndices.Count);

            int width = _orchestrator.Config.Width;
            int height = _orchestrator.Config.Height;
            int seed = _unpaintedCellIndices[UnityEngine.Random.Range(0, _unpaintedCellIndices.Count)];
            int seedX = seed % width;
            int seedY = seed / width;
            float centerU = (seedX + UnityEngine.Random.Range(0.25f, 0.75f)) / width;
            float centerV = (seedY + UnityEngine.Random.Range(0.25f, 0.75f)) / height;
            _unpaintedCellIndices.Sort((left, right) =>
            {
                int comparison = NormalizedDistanceSquared(left, centerU, centerV, width, height)
                    .CompareTo(NormalizedDistanceSquared(right, centerU, centerV, width, height));
                return comparison != 0 ? comparison : left.CompareTo(right);
            });

            double targetDistanceSquared = NormalizedDistanceSquared(_unpaintedCellIndices[targetCount - 1], centerU, centerV, width, height);
            int ringStart = targetCount - 1;
            while (ringStart > 0 && NearlyEqual(
                       NormalizedDistanceSquared(_unpaintedCellIndices[ringStart - 1], centerU, centerV, width, height),
                       targetDistanceSquared))
            {
                ringStart--;
            }
            int ringEnd = targetCount;
            while (ringEnd < _unpaintedCellIndices.Count && NearlyEqual(
                       NormalizedDistanceSquared(_unpaintedCellIndices[ringEnd], centerU, centerV, width, height),
                       targetDistanceSquared))
            {
                ringEnd++;
            }

            int minimumAllowed = Mathf.Clamp(Mathf.CeilToInt(_unpaintedCellIndices.Count * 0.08f), 1, _unpaintedCellIndices.Count);
            int maximumAllowed = Mathf.Clamp(Mathf.FloorToInt(_unpaintedCellIndices.Count * 0.12f), minimumAllowed, _unpaintedCellIndices.Count);
            bool usePreviousRing = ringStart >= minimumAllowed &&
                                   (ringEnd > maximumAllowed || targetCount - ringStart < ringEnd - targetCount);
            double selectedDistanceSquared;
            int predictedChangedCount;
            if (usePreviousRing)
            {
                double previousDistanceSquared = NormalizedDistanceSquared(_unpaintedCellIndices[ringStart - 1], centerU, centerV, width, height);
                selectedDistanceSquared = (previousDistanceSquared + targetDistanceSquared) * 0.5d;
                predictedChangedCount = ringStart;
            }
            else
            {
                selectedDistanceSquared = targetDistanceSquared + 1e-12d;
                predictedChangedCount = ringEnd;
            }

            if (!CanSubmit()) return false;
            int paintedBefore = TotalCellCount - _unpaintedCellIndices.Count;
            int revisionBefore = _orchestrator.VisualRevision;
            float radius = Mathf.Max(0.49f / Mathf.Max(width, height), (float)Math.Sqrt(selectedDistanceSquared) + (usePreviousRing ? 0f : 1e-6f));
            int strokeId = _orchestrator.CreateStrokeId();
            result = _orchestrator.RequestStamp(centerU, centerV, radius, true, strokeId);
            bool accepted = ConsumeResult(result, false) && (result.PaintedCellDelta > 0 || result.ChemicalOverlayRemovedCount > 0);
            LogChemicalStroke(strokeId, result.ChemicalOverlayRemovedCount, result.ChemicalBaseRemovedCount, result.SameStrokeChemicalBaseRemovedCount);
            Debug.Log($"[Basket][ContinuousStamp] center=({centerU:F4},{centerV:F4}), targetRatio={fraction:F4}, targetArea={targetCount}, selectedRadius={radius:F5}, changedArea={(TotalCellCount > 0 ? (double)result.PaintedCellDelta / TotalCellCount : 0d):F6}, changedCellCount={result.PaintedCellDelta}, predictedChangedCellCount={predictedChangedCount}, paintedBefore={paintedBefore}, paintedAfter={PaintedCellCount}, maskRevisionBefore={revisionBefore}, maskRevisionAfter={_orchestrator.VisualRevision}, maskTextureInstanceId={(_orchestrator.MaskTexture ? _orchestrator.MaskTexture.GetInstanceID() : 0)}, visualRefreshCalled={_lastVisualRefreshCalled}, completionTriggered={result.ClearThresholdReached}", this);
            return accepted;
        }

        private static double NormalizedDistanceSquared(int index, float centerU, float centerV, int width, int height)
        {
            double deltaX = (index % width + 0.5d) / width - centerU;
            double deltaY = (index / width + 0.5d) / height - centerV;
            return deltaX * deltaX + deltaY * deltaY;
        }

        private static bool NearlyEqual(double left, double right)
        {
            return Math.Abs(left - right) <= 1e-12d;
        }

        public static bool TryGetPreset(GameDifficulty difficulty, out Phase2PaintConfig config, out float radiusRatio)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy: config = Phase2PaintPresets.Easy; radiusRatio = 0.085f; return true;
                case GameDifficulty.Normal: config = Phase2PaintPresets.Normal; radiusRatio = 0.075f; return true;
                case GameDifficulty.Hard: config = Phase2PaintPresets.Hard; radiusRatio = 0.065f; return true;
                default: config = null; radiusRatio = 0f; return false;
            }
        }

        private bool CanSubmit()
        {
            return IsPrepared && IsRunning && !IsCleared && !IsExitReady && InputEnabled && _orchestrator != null;
        }

        private bool ConsumeResult(Phase2OrchestrationResult result, bool notifyGameplayInput = true)
        {
            _lastVisualRefreshCalled = result.VisualSubmittedCount > 0 && maskPresenter && maskPresenter.RefreshBoundMask(_orchestrator.MaskTexture);
            if ((result.ChemicalOverlayRemovedCount > 0 || result.ChemicalBaseRemovedCount > 0) &&
                (!maskPresenter || !maskPresenter.RefreshChemicalOverlay(_orchestrator.Grid)))
            {
                LastFailureReason = "Phase 2 chemical overlay refresh failed.";
                SetInputEnabled(false);
                return false;
            }
            if (result.FailureReason != Phase2OrchestrationFailureReason.None)
            {
                LastFailureReason = result.FailureReason.ToString();
                SetInputEnabled(false);
                pointerInput?.CancelActiveStroke();
                return false;
            }

            if (result.ClearThresholdReached && !IsCleared)
            {
                IsCleared = true;
                IsRunning = false;
                SetInputEnabled(false);
                pointerInput?.CancelActiveStroke();
                if (!_phaseClearedRaised)
                {
                    _phaseClearedRaised = true;
                    PhaseCleared?.Invoke();
                }
            }

            if (result.ScoreDelta > 0 && !scoreController.AddScore(result.ScoreDelta, GamePhaseId.Phase2, ScoreReason.Other))
            {
                LastFailureReason = "GameScoreController rejected a Phase 2 score delta.";
                SetInputEnabled(false);
                return false;
            }

            if (result.ClearThresholdReached && !_completionStarted)
            {
                _completionStarted = true;
                if (!maskPresenter.BeginCompletion(CompletionDurationSeconds, OnVisualCompletionFinished))
                {
                    _completionStarted = false;
                    LastFailureReason = "Phase 2 visual completion could not start.";
                    return false;
                }
            }
            bool accepted = result.LogicAcceptedCount > 0;
            if (accepted && notifyGameplayInput) GameplayInputActivity.NotifyValidGameplayInput(GamePhaseId.Phase2);
            return accepted;
        }

        private void OnVisualCompletionFinished()
        {
            _completionStarted = false;
            if (!IsCleared || _exitReadyRaised) return;
            Debug.Log($"[Phase2] visual completion finished painted={PaintedCellCount}/{TotalCellCount}, score={PhaseScore}", this);
            _exitReadyRaised = true;
            IsExitReady = true;
            PhaseExitReady?.Invoke();
        }

        private bool FailPrepare(string reason)
        {
            LastFailureReason = reason;
            SetInputEnabled(false);
            pointerInput?.CancelActiveStroke();
            maskPresenter?.ReleaseBinding();
            DisposeRuntime();
            IsPrepared = false;
            IsRunning = false;
            IsCleared = false;
            IsExitReady = false;
            gameObject.SetActive(false);
            return false;
        }

        private void OnDisable()
        {
            RestoreBaseBrushRadius();
            SetInputEnabled(false);
            pointerInput?.CancelActiveStroke();
            IsRunning = false;
            maskPresenter?.ReleaseBinding();
        }

        private void OnDestroy()
        {
            SetInputEnabled(false);
            pointerInput?.CancelActiveStroke();
            maskPresenter?.ReleaseBinding();
            DisposeRuntime();
        }

        private void DisposeRuntime()
        {
            EndActiveStroke();
            _orchestrator?.Dispose();
            _orchestrator = null;
        }

        private void AccumulateChemicalStroke(Phase2OrchestrationResult result)
        {
            _strokeOverlayRemoved += result.ChemicalOverlayRemovedCount;
            _strokeChemicalBaseRemoved += result.ChemicalBaseRemovedCount;
            _strokeSameStrokeBaseRemoved += result.SameStrokeChemicalBaseRemovedCount;
        }

        private void LogChemicalStroke(int strokeId, int overlayRemoved, int baseRemoved, int sameStrokeBaseRemoved)
        {
            if (!_chemicalEffectActive || _orchestrator == null) return;
            Debug.Log($"[RequestEffect][ChemicalOverlay] strokeId={strokeId}, overlayRemoved={overlayRemoved}, remainingOverlay={_orchestrator.RemainingChemicalOverlayCount}, sameStrokeBaseRemoved={sameStrokeBaseRemoved}", this);
            Debug.Log($"[RequestEffect][ChemicalBase] strokeId={strokeId}, baseRemoved={baseRemoved}, unfinishedChemical={_orchestrator.UnfinishedChemicalCellCount}", this);
        }

        private bool ApplyDeckSprite()
        {
            if (!deckPanel && !deckSprite) return true;
            if (!deckPanel || !deckSprite) return false;
            deckPanel.sprite = deckSprite;
            deckPanel.preserveAspect = true;
            return true;
        }
    }
}
