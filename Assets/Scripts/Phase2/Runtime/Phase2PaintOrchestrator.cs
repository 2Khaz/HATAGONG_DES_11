using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HATAGONG.Phase2.Rendering;

namespace HATAGONG.Phase2
{
    public sealed class Phase2PaintOrchestrator : IDisposable
    {
        private const int DefaultHistoryCapacity = 2048;
        public const int MaximumBatchStampCount = 65536;
        public const int VisualReplayChunkSize = 4096;

        private readonly Phase2PaintConfig _config;
        private readonly Phase2PaintSessionModel _session;
        private readonly Phase2PaintMaskRenderer _renderer;
        private readonly Phase2StampInterpolator _interpolator;
        private readonly List<(float u, float v)> _interpolatedPoints;
        private readonly List<Phase2PaintStamp> _visualBatch;
        private readonly List<Phase2PaintStamp> _visualHistory;
        private readonly ReadOnlyCollection<Phase2PaintStamp> _visualHistoryView;
        private readonly int _renderResolution;
        private bool _isPrepared;
        private bool _isReleased;
        private bool _visualRecoveryRequired;
        private int _nextStrokeId;

        public Phase2PaintOrchestrator(
            Phase2PaintConfig config,
            int renderResolution = Phase2PaintMaskRenderer.DefaultResolution,
            int historyCapacity = DefaultHistoryCapacity,
            int? chemicalSeed = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (renderResolution <= 0) throw new ArgumentOutOfRangeException(nameof(renderResolution));
            if (historyCapacity < 0) throw new ArgumentOutOfRangeException(nameof(historyCapacity));
            _renderResolution = renderResolution;
            _session = new Phase2PaintSessionModel(config, chemicalSeed);
            _renderer = new Phase2PaintMaskRenderer();
            _interpolator = new Phase2StampInterpolator();
            _interpolatedPoints = new List<(float u, float v)>(128);
            _visualBatch = new List<Phase2PaintStamp>(128);
            _visualHistory = new List<Phase2PaintStamp>(historyCapacity);
            _visualHistoryView = _visualHistory.AsReadOnly();
        }

        public Phase2PaintConfig Config => _config;
        public Phase2PaintSessionState CurrentState => _session.CurrentState;
        public int PaintedCellCount => _session.Grid.PaintedCellCount;
        public int Score => _session.Score;
        public Phase2MilestoneFlags Milestones => _session.Milestones;
        public UnityEngine.RenderTexture MaskTexture => _renderer.MaskTexture;
        public UnityEngine.Experimental.Rendering.GraphicsFormat MaskFormat => _renderer.SelectedFormat;
        public bool RendererInitialized => _renderer.IsInitialized;
        public int OwnedRenderTextureCount => _renderer.OwnedRenderTextureCount;
        public int VisualRevision => _renderer.Revision;
        public IReadOnlyList<Phase2PaintStamp> VisualHistory => _visualHistoryView;
        public int VisualHistoryCount => _visualHistory.Count;
        public int ChemicalCellCount => _session.Grid.ChemicalCellCount;
        public int RemainingChemicalOverlayCount => _session.Grid.RemainingChemicalOverlayCount;
        public int UnfinishedChemicalCellCount => _session.Grid.UnfinishedChemicalCellCount;
        public Phase2PaintGrid Grid => _session.Grid;
        public bool IsPrepared => _isPrepared && !_isReleased && _renderer.IsInitialized;
        public bool InputEnabled { get; private set; }

        public void CollectUnpaintedCellIndices(List<int> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            for (int i = 0; i < _session.Grid.TotalCellCount; i++)
                if (!_session.Grid.IsPainted(i)) destination.Add(i);
        }

        public bool Prepare()
        {
            if (!_config.IsValid(out _))
            {
                return false;
            }

            _isReleased = false;
            if (!_renderer.Initialize(_renderResolution))
            {
                _isPrepared = false;
                return false;
            }

            _session.Reset();
            _renderer.ResetMask();
            _visualHistory.Clear();
            ClearTransientBuffers();
            InputEnabled = false;
            _isPrepared = true;
            _visualRecoveryRequired = false;
            return true;
        }

        public bool StartRunning()
        {
            if (!IsPrepared || _session.CurrentState != Phase2PaintSessionState.Ready)
            {
                return false;
            }

            _session.Start();
            return _session.IsRunning;
        }

        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = IsPrepared && enabled;
        }

        public int CreateStrokeId()
        {
            _nextStrokeId = _nextStrokeId == int.MaxValue ? 1 : _nextStrokeId + 1;
            return _nextStrokeId;
        }

        public Phase2OrchestrationResult RequestStamp(float centerU, float centerV, float radiusRatio, bool sessionPlaying, int strokeId = 0)
        {
            _visualBatch.Clear();
            BatchAccumulator accumulator = CreateAccumulator(1);
            if (TryRejectForGate(1, sessionPlaying, ref accumulator))
            {
                return BuildResult(accumulator);
            }

            ProcessStamp(new Phase2PaintStamp(centerU, centerV, radiusRatio), sessionPlaying, strokeId == 0 ? CreateStrokeId() : strokeId, ref accumulator);
            SubmitVisualBatch(ref accumulator);
            return BuildResult(accumulator);
        }

        public Phase2OrchestrationResult RequestStampBatch(IReadOnlyList<Phase2PaintStamp> stamps, bool sessionPlaying, int strokeId = 0)
        {
            int inputCount = stamps?.Count ?? 0;
            _visualBatch.Clear();
            BatchAccumulator accumulator = CreateAccumulator(inputCount);
            if (inputCount == 0)
            {
                accumulator.FailureReason = Phase2OrchestrationFailureReason.InvalidInput;
                return BuildResult(accumulator);
            }
            if (inputCount > MaximumBatchStampCount)
            {
                accumulator.LogicRejectedCount = inputCount;
                accumulator.FailureReason = Phase2OrchestrationFailureReason.InputLimitExceeded;
                return BuildResult(accumulator);
            }
            if (TryRejectForGate(inputCount, sessionPlaying, ref accumulator))
            {
                return BuildResult(accumulator);
            }

            int effectiveStrokeId = strokeId == 0 ? CreateStrokeId() : strokeId;
            for (int i = 0; i < inputCount; i++)
            {
                ProcessStamp(stamps[i], sessionPlaying, effectiveStrokeId, ref accumulator);
            }
            SubmitVisualBatch(ref accumulator);
            return BuildResult(accumulator);
        }

        public Phase2OrchestrationResult RequestSegment(
            float startU,
            float startV,
            float endU,
            float endV,
            float radiusRatio,
            bool sessionPlaying,
            int strokeId = 0)
        {
            _visualBatch.Clear();
            _interpolatedPoints.Clear();
            if (!IsFinite(startU) || !IsFinite(startV) || !IsFinite(endU) || !IsFinite(endV) || !IsFinite(radiusRatio) || radiusRatio <= 0f)
            {
                BatchAccumulator invalid = CreateAccumulator(0);
                invalid.FailureReason = Phase2OrchestrationFailureReason.InvalidInput;
                return BuildResult(invalid);
            }

            float spacing = radiusRatio * (float)_config.StampSpacingRatio;
            if (!IsFinite(spacing) || spacing <= 0f)
            {
                BatchAccumulator invalid = CreateAccumulator(0);
                invalid.FailureReason = Phase2OrchestrationFailureReason.InvalidInput;
                return BuildResult(invalid);
            }

            double segmentDeltaU = (double)endU - startU;
            double segmentDeltaV = (double)endV - startV;
            double segmentLength = Math.Sqrt(segmentDeltaU * segmentDeltaU + segmentDeltaV * segmentDeltaV);
            double estimatedStampCount = Math.Ceiling(segmentLength / spacing) + 1d;
            if (estimatedStampCount > MaximumBatchStampCount)
            {
                int rejectedCount = estimatedStampCount >= int.MaxValue ? int.MaxValue : (int)estimatedStampCount;
                BatchAccumulator limited = CreateAccumulator(rejectedCount);
                limited.LogicRejectedCount = rejectedCount;
                limited.FailureReason = Phase2OrchestrationFailureReason.InputLimitExceeded;
                return BuildResult(limited);
            }

            try
            {
                _interpolator.Begin(startU, startV, _interpolatedPoints);
                _interpolator.AppendSegment(endU, endV, spacing, _interpolatedPoints);
                int lastIndex = _interpolatedPoints.Count - 1;
                if (lastIndex < 0)
                {
                    _interpolatedPoints.Add((endU, endV));
                }
                else if (IsWithinEndpointTolerance(_interpolatedPoints[lastIndex], endU, endV, spacing))
                {
                    _interpolatedPoints[lastIndex] = (endU, endV);
                }
                else
                {
                    _interpolatedPoints.Add((endU, endV));
                }
            }
            finally
            {
                _interpolator.End();
            }

            int inputCount = _interpolatedPoints.Count;
            if (inputCount > MaximumBatchStampCount)
            {
                BatchAccumulator limited = CreateAccumulator(inputCount);
                limited.LogicRejectedCount = inputCount;
                limited.FailureReason = Phase2OrchestrationFailureReason.InputLimitExceeded;
                return BuildResult(limited);
            }
            BatchAccumulator accumulator = CreateAccumulator(inputCount);
            if (TryRejectForGate(inputCount, sessionPlaying, ref accumulator))
            {
                return BuildResult(accumulator);
            }

            int effectiveStrokeId = strokeId == 0 ? CreateStrokeId() : strokeId;
            for (int i = 0; i < inputCount; i++)
            {
                (float u, float v) point = _interpolatedPoints[i];
                ProcessStamp(new Phase2PaintStamp(point.u, point.v, radiusRatio), sessionPlaying, effectiveStrokeId, ref accumulator);
            }
            SubmitVisualBatch(ref accumulator);
            return BuildResult(accumulator);
        }

        public bool Reset()
        {
            if (_isReleased)
            {
                return false;
            }

            if (!_renderer.IsInitialized && !_renderer.Initialize(_renderResolution))
            {
                _isPrepared = false;
                return false;
            }

            _session.Reset();
            _renderer.ResetMask();
            _visualHistory.Clear();
            ClearTransientBuffers();
            InputEnabled = false;
            _isPrepared = true;
            _visualRecoveryRequired = false;
            return true;
        }

        public Phase2VisualRecoveryResult RecoverVisualMaskIfNeeded()
        {
            if (_isReleased || !_isPrepared)
            {
                Phase2OrchestrationFailureReason reason = _isReleased
                    ? Phase2OrchestrationFailureReason.Released
                    : Phase2OrchestrationFailureReason.NotPrepared;
                return Phase2VisualRecoveryResult.Failed(false, false, 0, 0, reason);
            }
            bool rendererLost = !_renderer.IsInitialized;
            if (!rendererLost && !_visualRecoveryRequired)
            {
                return Phase2VisualRecoveryResult.Success(false, false, 0, 0);
            }

            int paintedBefore = _session.Grid.PaintedCellCount;
            int scoreBefore = _session.Score;
            Phase2MilestoneFlags milestonesBefore = _session.Milestones;
            Phase2PaintSessionState stateBefore = _session.CurrentState;
            int historyBefore = _visualHistory.Count;

            int replayedCount = 0;
            int replayBatchCount = 0;
            try
            {
                if (rendererLost && !_renderer.Initialize(_renderResolution))
                {
                    _visualRecoveryRequired = true;
                    return Phase2VisualRecoveryResult.Failed(true, true, 0, 0, Phase2OrchestrationFailureReason.VisualRecoveryFailed);
                }

                _renderer.ResetMask();
                _visualBatch.Clear();
                for (int i = 0; i < _visualHistory.Count; i++)
                {
                    _visualBatch.Add(_visualHistory[i]);
                    bool chunkReady = _visualBatch.Count == VisualReplayChunkSize || i == _visualHistory.Count - 1;
                    if (!chunkReady)
                    {
                        continue;
                    }

                    Phase2PaintBatchResult replay = _renderer.ApplyStampBatch(_visualBatch);
                    replayBatchCount++;
                    replayedCount += replay.AcceptedCount;
                    if (!replay.GpuSubmitted || replay.AcceptedCount != _visualBatch.Count || replay.RejectedCount != 0)
                    {
                        _visualRecoveryRequired = true;
                        return Phase2VisualRecoveryResult.Failed(true, true, replayedCount, replayBatchCount, Phase2OrchestrationFailureReason.VisualRecoveryFailed);
                    }
                    _visualBatch.Clear();
                }
            }
            catch (Exception)
            {
                _visualRecoveryRequired = true;
                return Phase2VisualRecoveryResult.Failed(true, true, replayedCount, replayBatchCount, Phase2OrchestrationFailureReason.VisualRecoveryFailed);
            }

            bool logicUnchanged = paintedBefore == _session.Grid.PaintedCellCount &&
                                  scoreBefore == _session.Score &&
                                  milestonesBefore == _session.Milestones &&
                                  stateBefore == _session.CurrentState &&
                                  historyBefore == _visualHistory.Count;
            if (!logicUnchanged)
            {
                _visualRecoveryRequired = true;
                return Phase2VisualRecoveryResult.Failed(true, true, replayedCount, replayBatchCount, Phase2OrchestrationFailureReason.VisualRecoveryFailed);
            }

            _visualRecoveryRequired = false;
            return Phase2VisualRecoveryResult.Success(true, true, replayedCount, replayBatchCount);
        }

        public void Release()
        {
            _session.Reset();
            _renderer.Release();
            _visualHistory.Clear();
            ClearTransientBuffers();
            InputEnabled = false;
            _isPrepared = false;
            _isReleased = true;
            _visualRecoveryRequired = false;
        }

        public void Dispose()
        {
            Release();
        }

        private BatchAccumulator CreateAccumulator(int inputCount)
        {
            return new BatchAccumulator
            {
                InputStampCount = inputCount,
                StateBefore = _session.CurrentState,
                StateAfter = _session.CurrentState,
                FirstLogicRejectionReason = Phase2PaintMutationRejectionReason.None,
                FailureReason = Phase2OrchestrationFailureReason.None
            };
        }

        private bool TryRejectForGate(int inputCount, bool sessionPlaying, ref BatchAccumulator accumulator)
        {
            if (_isReleased)
            {
                accumulator.LogicRejectedCount = inputCount;
                accumulator.FailureReason = Phase2OrchestrationFailureReason.Released;
                return true;
            }
            if (!_isPrepared)
            {
                accumulator.LogicRejectedCount = inputCount;
                accumulator.FailureReason = Phase2OrchestrationFailureReason.NotPrepared;
                return true;
            }
            if (!sessionPlaying)
            {
                RejectAll(inputCount, Phase2PaintMutationRejectionReason.SessionNotPlaying, ref accumulator);
                return true;
            }
            if (!InputEnabled)
            {
                RejectAll(inputCount, Phase2PaintMutationRejectionReason.InputDisabled, ref accumulator);
                return true;
            }
            if (!_session.IsRunning)
            {
                Phase2PaintMutationRejectionReason reason = _session.IsCompleting || _session.IsCleared
                    ? Phase2PaintMutationRejectionReason.AlreadyCompleting
                    : Phase2PaintMutationRejectionReason.PhaseNotRunning;
                RejectAll(inputCount, reason, ref accumulator);
                return true;
            }
            if (!_renderer.IsInitialized)
            {
                accumulator.LogicRejectedCount = inputCount;
                accumulator.FailureReason = Phase2OrchestrationFailureReason.VisualNotReady;
                return true;
            }
            return false;
        }

        private static void RejectAll(int inputCount, Phase2PaintMutationRejectionReason reason, ref BatchAccumulator accumulator)
        {
            accumulator.LogicRejectedCount = inputCount;
            accumulator.FirstLogicRejectionReason = reason;
        }

        private void ProcessStamp(Phase2PaintStamp stamp, bool sessionPlaying, int strokeId, ref BatchAccumulator accumulator)
        {
            if (!IsFinite(stamp.CenterU) || !IsFinite(stamp.CenterV) || !IsFinite(stamp.RadiusRatio) || stamp.RadiusRatio <= 0f)
            {
                RecordLogicRejection(Phase2PaintMutationRejectionReason.InvalidStamp, ref accumulator);
                return;
            }
            if (!Phase2PaintGeometry.IsCircleIntersectingUnitBoard(stamp.CenterU, stamp.CenterV, stamp.RadiusRatio))
            {
                RecordLogicRejection(Phase2PaintMutationRejectionReason.NoBoardIntersection, ref accumulator);
                return;
            }

            Phase2StampResult result = _session.ApplyStamp(stamp.CenterU, stamp.CenterV, stamp.RadiusRatio, sessionPlaying, InputEnabled, strokeId);
            accumulator.StateAfter = result.StateAfter;
            if (!result.Accepted)
            {
                RecordLogicRejection(result.RejectionReason, ref accumulator);
                return;
            }

            accumulator.LogicAcceptedCount++;
            accumulator.ScoreDelta += result.TotalScoreDelta;
            accumulator.PaintedCellDelta += result.NewlyPaintedCellCount;
            accumulator.ReachedMilestones |= result.ReachedMilestones;
            accumulator.ClearThresholdReached |= result.ClearThresholdReached;
            accumulator.ChemicalOverlayRemovedCount += _session.Grid.LastOverlayRemovedCount;
            accumulator.ChemicalBaseRemovedCount += _session.Grid.LastChemicalBaseRemovedCount;
            accumulator.SameStrokeChemicalBaseRemovedCount += _session.Grid.LastSameStrokeBaseRemovedCount;
            _visualBatch.Add(stamp);
            _visualHistory.Add(stamp);
            accumulator.HistoryAddedCount++;
        }

        private static void RecordLogicRejection(Phase2PaintMutationRejectionReason reason, ref BatchAccumulator accumulator)
        {
            accumulator.LogicRejectedCount++;
            if (accumulator.FirstLogicRejectionReason == Phase2PaintMutationRejectionReason.None)
            {
                accumulator.FirstLogicRejectionReason = reason;
            }
        }

        private void SubmitVisualBatch(ref BatchAccumulator accumulator)
        {
            accumulator.StateAfter = _session.CurrentState;
            if (_visualBatch.Count == 0)
            {
                return;
            }

            try
            {
                Phase2PaintBatchResult visualResult = _renderer.ApplyStampBatch(_visualBatch);
                accumulator.VisualSubmittedCount = visualResult.GpuSubmitted ? visualResult.AcceptedCount : 0;
                if (!visualResult.GpuSubmitted || visualResult.AcceptedCount != _visualBatch.Count || visualResult.RejectedCount != 0)
                {
                    accumulator.FailureReason = Phase2OrchestrationFailureReason.VisualSubmissionFailed;
                    _visualRecoveryRequired = true;
                }
            }
            catch (Exception)
            {
                accumulator.VisualSubmittedCount = 0;
                accumulator.FailureReason = Phase2OrchestrationFailureReason.VisualSubmissionFailed;
                _visualRecoveryRequired = true;
            }
        }

        private Phase2OrchestrationResult BuildResult(BatchAccumulator accumulator)
        {
            accumulator.StateAfter = _session.CurrentState;
            double progress = Phase2PaintProgressRules.CalculateCoverage(_session.Grid.PaintedCellCount, _config.TotalCellCount);
            int percent = (int)Math.Floor(progress * 100d);
            return new Phase2OrchestrationResult(
                accumulator.InputStampCount,
                accumulator.LogicAcceptedCount,
                accumulator.LogicRejectedCount,
                accumulator.VisualSubmittedCount,
                accumulator.HistoryAddedCount,
                accumulator.ScoreDelta,
                accumulator.PaintedCellDelta,
                progress,
                percent,
                accumulator.ReachedMilestones,
                accumulator.ClearThresholdReached,
                accumulator.ChemicalOverlayRemovedCount,
                accumulator.ChemicalBaseRemovedCount,
                accumulator.SameStrokeChemicalBaseRemovedCount,
                _session.Grid.RemainingChemicalOverlayCount,
                _session.Grid.UnfinishedChemicalCellCount,
                accumulator.StateBefore,
                accumulator.StateAfter,
                accumulator.FirstLogicRejectionReason,
                accumulator.FailureReason);
        }

        private static bool IsWithinEndpointTolerance((float u, float v) point, float endU, float endV, float spacing)
        {
            double magnitude = Math.Max(1d, Math.Max(Math.Abs(endU), Math.Abs(endV)));
            double floatTolerance = 8d * 1.1920928955078125e-7d * magnitude;
            double spacingTolerance = (double)spacing * 0.01d;
            double tolerance = Math.Min(floatTolerance, spacingTolerance);
            double deltaU = (double)point.u - endU;
            double deltaV = (double)point.v - endV;
            return deltaU * deltaU + deltaV * deltaV <= tolerance * tolerance;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void ClearTransientBuffers()
        {
            _interpolator.End();
            _interpolatedPoints.Clear();
            _visualBatch.Clear();
        }

        private struct BatchAccumulator
        {
            public int InputStampCount;
            public int LogicAcceptedCount;
            public int LogicRejectedCount;
            public int VisualSubmittedCount;
            public int HistoryAddedCount;
            public int ScoreDelta;
            public int PaintedCellDelta;
            public Phase2MilestoneFlags ReachedMilestones;
            public bool ClearThresholdReached;
            public int ChemicalOverlayRemovedCount;
            public int ChemicalBaseRemovedCount;
            public int SameStrokeChemicalBaseRemovedCount;
            public Phase2PaintSessionState StateBefore;
            public Phase2PaintSessionState StateAfter;
            public Phase2PaintMutationRejectionReason FirstLogicRejectionReason;
            public Phase2OrchestrationFailureReason FailureReason;
        }
    }

    public readonly struct Phase2OrchestrationResult
    {
        public Phase2OrchestrationResult(
            int inputStampCount,
            int logicAcceptedCount,
            int logicRejectedCount,
            int visualSubmittedCount,
            int historyAddedCount,
            int scoreDelta,
            int paintedCellDelta,
            double progress01,
            int percent,
            Phase2MilestoneFlags reachedMilestones,
            bool clearThresholdReached,
            int chemicalOverlayRemovedCount,
            int chemicalBaseRemovedCount,
            int sameStrokeChemicalBaseRemovedCount,
            int remainingChemicalOverlayCount,
            int unfinishedChemicalCellCount,
            Phase2PaintSessionState stateBefore,
            Phase2PaintSessionState stateAfter,
            Phase2PaintMutationRejectionReason firstLogicRejectionReason,
            Phase2OrchestrationFailureReason failureReason)
        {
            InputStampCount = inputStampCount;
            LogicAcceptedCount = logicAcceptedCount;
            LogicRejectedCount = logicRejectedCount;
            VisualSubmittedCount = visualSubmittedCount;
            HistoryAddedCount = historyAddedCount;
            ScoreDelta = scoreDelta;
            PaintedCellDelta = paintedCellDelta;
            Progress01 = progress01;
            Percent = percent;
            ReachedMilestones = reachedMilestones;
            ClearThresholdReached = clearThresholdReached;
            ChemicalOverlayRemovedCount = chemicalOverlayRemovedCount;
            ChemicalBaseRemovedCount = chemicalBaseRemovedCount;
            SameStrokeChemicalBaseRemovedCount = sameStrokeChemicalBaseRemovedCount;
            RemainingChemicalOverlayCount = remainingChemicalOverlayCount;
            UnfinishedChemicalCellCount = unfinishedChemicalCellCount;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
            FirstLogicRejectionReason = firstLogicRejectionReason;
            FailureReason = failureReason;
        }

        public int InputStampCount { get; }
        public int LogicAcceptedCount { get; }
        public int LogicRejectedCount { get; }
        public int VisualSubmittedCount { get; }
        public int HistoryAddedCount { get; }
        public int ScoreDelta { get; }
        public int PaintedCellDelta { get; }
        public double Progress01 { get; }
        public int Percent { get; }
        public Phase2MilestoneFlags ReachedMilestones { get; }
        public bool ClearThresholdReached { get; }
        public int ChemicalOverlayRemovedCount { get; }
        public int ChemicalBaseRemovedCount { get; }
        public int SameStrokeChemicalBaseRemovedCount { get; }
        public int RemainingChemicalOverlayCount { get; }
        public int UnfinishedChemicalCellCount { get; }
        public Phase2PaintSessionState StateBefore { get; }
        public Phase2PaintSessionState StateAfter { get; }
        public Phase2PaintMutationRejectionReason FirstLogicRejectionReason { get; }
        public Phase2OrchestrationFailureReason FailureReason { get; }
    }

    public readonly struct Phase2VisualRecoveryResult
    {
        private Phase2VisualRecoveryResult(bool wasRequired, bool succeeded, bool shouldCancelActiveInput, int replayedStampCount, int replayBatchCount, Phase2OrchestrationFailureReason failureReason)
        {
            WasRequired = wasRequired;
            Succeeded = succeeded;
            ShouldCancelActiveInput = shouldCancelActiveInput;
            ReplayedStampCount = replayedStampCount;
            ReplayBatchCount = replayBatchCount;
            FailureReason = failureReason;
        }

        public bool WasRequired { get; }
        public bool Succeeded { get; }
        public bool ShouldCancelActiveInput { get; }
        public int ReplayedStampCount { get; }
        public int ReplayBatchCount { get; }
        public Phase2OrchestrationFailureReason FailureReason { get; }

        public static Phase2VisualRecoveryResult Success(bool wasRequired, bool shouldCancelActiveInput, int replayedStampCount, int replayBatchCount)
        {
            return new Phase2VisualRecoveryResult(wasRequired, true, shouldCancelActiveInput, replayedStampCount, replayBatchCount, Phase2OrchestrationFailureReason.None);
        }

        public static Phase2VisualRecoveryResult Failed(bool wasRequired, bool shouldCancelActiveInput, int replayedStampCount, int replayBatchCount, Phase2OrchestrationFailureReason failureReason)
        {
            return new Phase2VisualRecoveryResult(wasRequired, false, shouldCancelActiveInput, replayedStampCount, replayBatchCount, failureReason);
        }
    }

    public enum Phase2OrchestrationFailureReason
    {
        None,
        NotPrepared,
        Released,
        InvalidInput,
        InputLimitExceeded,
        VisualNotReady,
        VisualSubmissionFailed,
        VisualRecoveryFailed
    }
}
