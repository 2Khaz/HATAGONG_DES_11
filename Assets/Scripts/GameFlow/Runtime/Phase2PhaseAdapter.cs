using System;
using System.Collections.Generic;
using HATAGONG.Phase2;
using HATAGONG.Phase2.Rendering;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HATAGONG.GameFlow
{
    public sealed class Phase2PhaseAdapter : MonoBehaviour, IGamePhase
    {
        public const float CompletionDurationSeconds = 0.4f;
        [SerializeField] private Phase2MaskPresenter maskPresenter;
        [SerializeField] private Phase2PointerInputController pointerInput;
        [SerializeField] private GameScoreController scoreController;

        private Phase2PaintOrchestrator _orchestrator;
        private float _brushRadiusRatio;
        private bool _phaseClearedRaised;
        private bool _exitReadyRaised;
        private bool _completionStarted;

        public GamePhaseId PhaseId => GamePhaseId.Phase2;
        public bool IsPrepared { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsExitReady { get; private set; }
        public bool InputEnabled => pointerInput && pointerInput.InputEnabled;
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

        public event Action PhaseCleared;
        public event Action PhaseExitReady;

        public bool Prepare(GameRunContext context)
        {
            SetInputEnabled(false);
            pointerInput?.CancelActiveStroke();
            if (gameObject.activeSelf) gameObject.SetActive(false);
            LastFailureReason = string.Empty;

            if (!context.IsValid || !maskPresenter || !pointerInput || !scoreController ||
                !TryGetPreset(context.Difficulty, out Phase2PaintConfig config, out float radius))
            {
                return FailPrepare("Invalid context or required Phase 2 scene reference.");
            }

            DisposeRuntime();
            try
            {
                _orchestrator = new Phase2PaintOrchestrator(config);
                if (!_orchestrator.Prepare())
                {
                    return FailPrepare("Phase 2 orchestrator preparation failed.");
                }
                _brushRadiusRatio = radius;
                if (!maskPresenter.Bind(_orchestrator.MaskTexture))
                {
                    return FailPrepare("Phase 2 mask presenter binding failed.");
                }
                IsPrepared = true;
                IsRunning = false;
                IsCleared = false;
                IsExitReady = false;
                _phaseClearedRaised = false;
                _exitReadyRaised = false;
                _completionStarted = false;
                RuntimeGenerationCount++;
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
            if (!IsPrepared || _orchestrator == null || !maskPresenter || !pointerInput)
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
            result = _orchestrator.RequestStamp(boardUv.x, boardUv.y, _brushRadiusRatio, true);
            return ConsumeResult(result);
        }

        public bool TryContinueStroke(Vector2 startUv, Vector2 endUv, out Phase2OrchestrationResult result)
        {
            if (!CanSubmit())
            {
                result = default;
                return false;
            }
            result = _orchestrator.RequestSegment(startUv.x, startUv.y, endUv.x, endUv.y, _brushRadiusRatio, true);
            return ConsumeResult(result);
        }

        public bool TryApplyStampBatch(IReadOnlyList<Phase2PaintStamp> stamps, out Phase2OrchestrationResult result)
        {
            if (!CanSubmit())
            {
                result = default;
                return false;
            }
            result = _orchestrator.RequestStampBatch(stamps, true);
            return ConsumeResult(result);
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

        private bool ConsumeResult(Phase2OrchestrationResult result)
        {
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
            return result.LogicAcceptedCount > 0;
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
            _orchestrator?.Dispose();
            _orchestrator = null;
        }
    }
}
