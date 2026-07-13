#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HATAGONG.Phase2.Rendering;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase2.Editor
{
    public static class Phase2RuntimeOrchestrationValidation
    {
        [MenuItem("Tools/HATAGONG/Phase2/Validate Runtime Orchestration Core")]
        public static void Validate()
        {
            Debug.Log("[Phase2Orchestration][Test] validation started");
            int passed = 0;
            int total = 0;

            void Check(bool condition, string name)
            {
                total++;
                if (!condition)
                {
                    throw new InvalidOperationException("[Phase2Orchestration][Test] " + name);
                }
                passed++;
            }

            var orchestrator = new Phase2PaintOrchestrator(Phase2PaintConfig.CreateProduction());
            Texture2D readback = null;
            try
            {
                Check(orchestrator.Prepare(), "prepare succeeds");
                Check(orchestrator.Session.CurrentState == Phase2PaintSessionState.Ready && orchestrator.Session.Grid.PaintedCellCount == 0 && orchestrator.Session.Score == 0, "prepare resets logic");
                Check(orchestrator.VisualHistoryCount == 0 && orchestrator.MaskRenderer.IsInitialized, "prepare initializes visual ownership");
                readback = ReadMask(orchestrator.MaskRenderer.MaskTexture, readback);
                Check(MaximumRed(readback) == 0, "prepare clears mask");

                var interpolator = new Phase2StampInterpolator();
                var interpolationBuffer = new List<(float u, float v)>();
                Check(interpolator.Begin(0f, 0f, interpolationBuffer) == 1 && interpolationBuffer.Count == 1, "interpolator begin emits start once");
                Check(interpolator.AppendSegment(0.05f, 0f, 0.1f, interpolationBuffer) == 0 && interpolationBuffer.Count == 1, "interpolator short segment carries distance without duplicate");
                Check(interpolator.AppendSegment(0.2f, 0f, 0.1f, interpolationBuffer) == 2 && interpolationBuffer.Count == 3 && Approximately(interpolationBuffer[1].u, 0.1f) && Approximately(interpolationBuffer[2].u, 0.2f), "interpolator accumulated distance has no omission");
                interpolator.End();
                interpolator.Begin(0.2f, 0.2f, interpolationBuffer);
                Check(interpolator.AppendSegment(0.2f, 0.2f, 0.1f, interpolationBuffer) == 0 && interpolationBuffer.Count == 1, "interpolator identical point has no duplicate");
                interpolator.End();
                interpolator.Begin(0f, 0f, interpolationBuffer);
                Check(interpolator.AppendSegment(0.35f, 0f, 0.1f, interpolationBuffer) == 3 && PointsStrictlyAdvance(interpolationBuffer), "interpolator long segment is ordered and unique");
                Check(interpolator.AppendSegment(0.4f, 0f, float.NaN, interpolationBuffer) == 0, "interpolator rejects non-finite spacing");
                interpolator.End();

                float nearLimitSpacing = 1f / (Phase2PaintOrchestrator.MaximumBatchStampCount - 32);
                float nearLimitRadius = nearLimitSpacing / (float)orchestrator.Config.StampSpacingRatio;
                var nearLimitSegment = orchestrator.RequestSegment(0f, 0f, 1f, 0f, nearLimitRadius, false);
                Check(nearLimitSegment.InputStampCount >= Phase2PaintOrchestrator.MaximumBatchStampCount - 64 && nearLimitSegment.InputStampCount <= Phase2PaintOrchestrator.MaximumBatchStampCount && nearLimitSegment.LogicRejectedCount == nearLimitSegment.InputStampCount, "segment limit boundary is fully accounted");
                Check(nearLimitSegment.FirstLogicRejectionReason == Phase2PaintMutationRejectionReason.SessionNotPlaying && NoMutation(orchestrator), "segment limit boundary preserves state when gated");

                var sessionBlocked = orchestrator.RequestStamp(0.5f, 0.5f, 0.05f, false);
                Check(sessionBlocked.LogicAcceptedCount == 0 && sessionBlocked.LogicRejectedCount == 1 && sessionBlocked.FirstLogicRejectionReason == Phase2PaintMutationRejectionReason.SessionNotPlaying, "session not playing rejected");
                Check(NoMutation(orchestrator), "session rejection preserves all state");

                var inputBlocked = orchestrator.RequestStamp(0.5f, 0.5f, 0.05f, true);
                Check(inputBlocked.LogicAcceptedCount == 0 && inputBlocked.LogicRejectedCount == 1 && inputBlocked.FirstLogicRejectionReason == Phase2PaintMutationRejectionReason.InputDisabled, "input disabled rejected");
                Check(NoMutation(orchestrator), "input rejection preserves all state");

                orchestrator.SetInputEnabled(true);
                var phaseBlocked = orchestrator.RequestStamp(0.5f, 0.5f, 0.05f, true);
                Check(phaseBlocked.LogicAcceptedCount == 0 && phaseBlocked.LogicRejectedCount == 1 && phaseBlocked.FirstLogicRejectionReason == Phase2PaintMutationRejectionReason.PhaseNotRunning, "internal ready state rejected");
                Check(NoMutation(orchestrator), "phase state rejection preserves all state");

                Check(orchestrator.StartRunning(), "start running");
                orchestrator.SetInputEnabled(true);
                var limitedSegment = orchestrator.RequestSegment(0f, 0f, 1f, 1f, 0.00000001f, true);
                Check(limitedSegment.FailureReason == Phase2OrchestrationFailureReason.InputLimitExceeded && limitedSegment.InputStampCount == limitedSegment.LogicRejectedCount && limitedSegment.LogicAcceptedCount == 0 && limitedSegment.VisualSubmittedCount == 0 && limitedSegment.HistoryAddedCount == 0, "segment input limit fails explicitly");
                Check(NoMutation(orchestrator), "segment input limit preserves all state");
                int scoreBeforeSingle = orchestrator.Session.Score;
                var single = orchestrator.RequestStamp(0.5f, 0.5f, 0.05f, true);
                Check(single.InputStampCount == 1 && single.LogicAcceptedCount == 1 && single.LogicRejectedCount == 0, "single stamp logic accepted");
                Check(single.PaintedCellDelta > 0 && orchestrator.Session.Grid.PaintedCellCount == single.PaintedCellDelta, "single stamp mutates grid once");
                Check(single.VisualSubmittedCount == 1 && single.HistoryAddedCount == 1 && orchestrator.VisualHistoryCount == 1, "single stamp visual and history match");
                Check(single.ScoreDelta == orchestrator.Session.Score - scoreBeforeSingle && single.FailureReason == Phase2OrchestrationFailureReason.None, "single stamp score result exact");
                readback = ReadMask(orchestrator.MaskRenderer.MaskTexture, readback);
                Check(SampleRed(readback, 0.5f, 0.5f) >= 0.98f, "single stamp visible in mask");

                int paintedBeforeOutside = orchestrator.Session.Grid.PaintedCellCount;
                int scoreBeforeOutside = orchestrator.Session.Score;
                int historyBeforeOutside = orchestrator.VisualHistoryCount;
                var outside = orchestrator.RequestStamp(-0.09f, -0.09f, 0.10f, true);
                Check(outside.LogicAcceptedCount == 0 && outside.LogicRejectedCount == 1 && outside.FirstLogicRejectionReason == Phase2PaintMutationRejectionReason.NoBoardIntersection, "circle outside board rejected");
                Check(outside.VisualSubmittedCount == 0 && outside.HistoryAddedCount == 0 && orchestrator.Session.Grid.PaintedCellCount == paintedBeforeOutside && orchestrator.Session.Score == scoreBeforeOutside && orchestrator.VisualHistoryCount == historyBeforeOutside, "outside rejection preserves logic visual history");

                int paintedBeforeDuplicate = orchestrator.Session.Grid.PaintedCellCount;
                int scoreBeforeDuplicate = orchestrator.Session.Score;
                var duplicate = orchestrator.RequestStamp(0.5f, 0.5f, 0.05f, true);
                Check(duplicate.LogicAcceptedCount == 1 && duplicate.PaintedCellDelta == 0 && orchestrator.Session.Grid.PaintedCellCount == paintedBeforeDuplicate, "duplicate follows logic new-cell rule");
                Check(duplicate.ScoreDelta == orchestrator.Session.Score - scoreBeforeDuplicate && duplicate.ScoreDelta == 0, "duplicate follows score rule");
                Check(duplicate.VisualSubmittedCount == 1 && duplicate.HistoryAddedCount == 1 && orchestrator.VisualHistoryCount == historyBeforeOutside + 1, "accepted duplicate preserves visual replay order");

                Check(orchestrator.Reset(), "reset before segment");
                Check(orchestrator.StartRunning(), "segment session starts");
                orchestrator.SetInputEnabled(true);
                int historyBeforeSegment = orchestrator.VisualHistoryCount;
                int scoreBeforeSegment = orchestrator.Session.Score;
                var segment = orchestrator.RequestSegment(0.1f, 0.1f, 0.4f, 0.1f, 0.05f, true);
                Check(segment.InputStampCount > 2 && segment.InputStampCount == segment.LogicAcceptedCount + segment.LogicRejectedCount, "segment accounts for every interpolated stamp");
                Check(segment.LogicRejectedCount == 0 && segment.VisualSubmittedCount == segment.LogicAcceptedCount && segment.HistoryAddedCount == segment.LogicAcceptedCount, "segment visual count matches logic accepted");
                Check(segment.ScoreDelta == orchestrator.Session.Score - scoreBeforeSegment && segment.PaintedCellDelta == orchestrator.Session.Grid.PaintedCellCount, "segment aggregates score and painted delta");
                Check(orchestrator.VisualHistoryCount == historyBeforeSegment + segment.HistoryAddedCount && HistoryIsStrictlyOrdered(orchestrator.VisualHistory, historyBeforeSegment), "segment history preserves interpolation order");
                Check(Approximately(orchestrator.VisualHistory[historyBeforeSegment].CenterU, 0.1f) && Approximately(orchestrator.VisualHistory[orchestrator.VisualHistoryCount - 1].CenterU, 0.4f), "orchestrated segment includes start and end exactly once");

                readback = ReadMask(orchestrator.MaskRenderer.MaskTexture, readback);
                Color32[] maskBeforeRecovery = readback.GetPixels32();
                int paintedBeforeRecovery = orchestrator.Session.Grid.PaintedCellCount;
                int scoreBeforeRecovery = orchestrator.Session.Score;
                Phase2MilestoneFlags milestonesBeforeRecovery = orchestrator.Session.Milestones;
                Phase2PaintSessionState stateBeforeRecovery = orchestrator.Session.CurrentState;
                int historyBeforeRecovery = orchestrator.VisualHistoryCount;
                orchestrator.MaskRenderer.MaskTexture.Release();
                var recovery = orchestrator.RecoverVisualMaskIfNeeded();
                Check(recovery.WasRequired && recovery.Succeeded && recovery.ShouldCancelActiveInput && recovery.ReplayedStampCount == historyBeforeRecovery, "lost render texture replays full history");
                Check(orchestrator.Session.Grid.PaintedCellCount == paintedBeforeRecovery && orchestrator.Session.Score == scoreBeforeRecovery && orchestrator.Session.Milestones == milestonesBeforeRecovery && orchestrator.Session.CurrentState == stateBeforeRecovery && orchestrator.VisualHistoryCount == historyBeforeRecovery, "history replay preserves logic and history");
                readback = ReadMask(orchestrator.MaskRenderer.MaskTexture, readback);
                Check(RedChannelsEqual(maskBeforeRecovery, readback.GetPixels32()), "history replay restores visual mask");

                Check(orchestrator.Reset(), "reset before threshold");
                Check(orchestrator.StartRunning(), "threshold session starts");
                orchestrator.SetInputEnabled(true);
                var cellStamps = new List<Phase2PaintStamp>(orchestrator.Config.RequiredClearCells - 1);
                for (int i = 0; i < orchestrator.Config.RequiredClearCells - 1; i++)
                {
                    int x = i % orchestrator.Config.Width;
                    int y = i / orchestrator.Config.Width;
                    cellStamps.Add(new Phase2PaintStamp(
                        (float)((x + 0.5d) / orchestrator.Config.Width),
                        (float)((y + 0.5d) / orchestrator.Config.Height),
                        0.0001f));
                }

                var belowThreshold = orchestrator.RequestStampBatch(cellStamps, true);
                Check(belowThreshold.InputStampCount == 16220 && belowThreshold.LogicAcceptedCount == 16220 && belowThreshold.VisualSubmittedCount == 16220 && belowThreshold.HistoryAddedCount == 16220, "16220 batch has no omissions");
                Check(orchestrator.Session.Grid.PaintedCellCount == 16220 && !belowThreshold.ClearThresholdReached && belowThreshold.StateAfter == Phase2PaintSessionState.Running, "16220 remains incomplete");
                Check(belowThreshold.ReachedMilestones == (Phase2MilestoneFlags.Quarter | Phase2MilestoneFlags.Half | Phase2MilestoneFlags.ThreeQuarter), "milestones each reported on first crossing");
                Check(belowThreshold.ScoreDelta == orchestrator.Session.Score, "threshold batch score sum exact");

                int thresholdIndex = orchestrator.Config.RequiredClearCells - 1;
                int thresholdX = thresholdIndex % orchestrator.Config.Width;
                int thresholdY = thresholdIndex / orchestrator.Config.Width;
                float thresholdU = (float)((thresholdX + 0.5d) / orchestrator.Config.Width);
                float thresholdV = (float)((thresholdY + 0.5d) / orchestrator.Config.Height);
                float nextU = (float)((thresholdX + 1.5d) / orchestrator.Config.Width);
                int scoreBeforeClearSegment = orchestrator.Session.Score;
                int historyBeforeClearSegment = orchestrator.VisualHistoryCount;
                var clearSegment = orchestrator.RequestSegment(thresholdU, thresholdV, nextU, thresholdV, 0.0001f, true);
                Check(clearSegment.InputStampCount > 1 && clearSegment.LogicAcceptedCount == 1 && clearSegment.LogicRejectedCount == clearSegment.InputStampCount - 1, "completing rejects remaining segment stamps");
                Check(clearSegment.VisualSubmittedCount == 1 && clearSegment.HistoryAddedCount == 1 && orchestrator.VisualHistoryCount == historyBeforeClearSegment + 1, "only clear stamp enters visual history");
                Check(clearSegment.PaintedCellDelta == 1 && orchestrator.Session.Grid.PaintedCellCount == 16221 && clearSegment.ClearThresholdReached, "16221 first clear exact");
                Check(clearSegment.StateBefore == Phase2PaintSessionState.Running && clearSegment.StateAfter == Phase2PaintSessionState.Completing && clearSegment.FirstLogicRejectionReason == Phase2PaintMutationRejectionReason.AlreadyCompleting, "clear segment state and rejection reason");
                Check(clearSegment.ScoreDelta == orchestrator.Session.Score - scoreBeforeClearSegment && clearSegment.ReachedMilestones == Phase2MilestoneFlags.None, "clear score exact and milestones not repeated");

                Check(orchestrator.Reset(), "reset before large history replay");
                Check(orchestrator.StartRunning(), "large history session starts");
                orchestrator.SetInputEnabled(true);
                var largeHistorySegment = orchestrator.RequestSegment(0f, 0f, 1f, 0f, nearLimitRadius, true);
                Check(largeHistorySegment.FailureReason == Phase2OrchestrationFailureReason.None && largeHistorySegment.InputStampCount <= Phase2PaintOrchestrator.MaximumBatchStampCount && largeHistorySegment.LogicAcceptedCount == largeHistorySegment.InputStampCount, "maximum-size segment is accepted without omission");
                int additionalHistoryCount = Phase2PaintOrchestrator.MaximumBatchStampCount - orchestrator.VisualHistoryCount + 1;
                var additionalHistory = new List<Phase2PaintStamp>(additionalHistoryCount);
                for (int i = 0; i < additionalHistoryCount; i++)
                {
                    additionalHistory.Add(new Phase2PaintStamp(0.5f, 0.5f, 0.0001f));
                }
                var additionalHistoryResult = orchestrator.RequestStampBatch(additionalHistory, true);
                Check(additionalHistoryResult.LogicAcceptedCount == additionalHistoryCount && additionalHistoryResult.HistoryAddedCount == additionalHistoryCount, "history grows beyond request limit explicitly");
                int largeHistoryCount = orchestrator.VisualHistoryCount;
                int largePaintedBeforeRecovery = orchestrator.Session.Grid.PaintedCellCount;
                int largeScoreBeforeRecovery = orchestrator.Session.Score;
                Phase2MilestoneFlags largeMilestonesBeforeRecovery = orchestrator.Session.Milestones;
                Phase2PaintSessionState largeStateBeforeRecovery = orchestrator.Session.CurrentState;
                orchestrator.MaskRenderer.MaskTexture.Release();
                var chunkedRecovery = orchestrator.RecoverVisualMaskIfNeeded();
                int expectedReplayBatches = (largeHistoryCount + Phase2PaintOrchestrator.VisualReplayChunkSize - 1) / Phase2PaintOrchestrator.VisualReplayChunkSize;
                Check(chunkedRecovery.Succeeded && chunkedRecovery.ReplayedStampCount == largeHistoryCount && chunkedRecovery.ReplayBatchCount == expectedReplayBatches && chunkedRecovery.ReplayBatchCount > 1, "large history replay uses reusable chunks without omission");
                Check(orchestrator.Session.Grid.PaintedCellCount == largePaintedBeforeRecovery && orchestrator.Session.Score == largeScoreBeforeRecovery && orchestrator.Session.Milestones == largeMilestonesBeforeRecovery && orchestrator.Session.CurrentState == largeStateBeforeRecovery && orchestrator.VisualHistoryCount == largeHistoryCount, "chunked replay preserves logic and history");

                Check(orchestrator.Reset(), "final reset succeeds");
                Check(orchestrator.Session.CurrentState == Phase2PaintSessionState.Ready && orchestrator.Session.Grid.PaintedCellCount == 0 && orchestrator.Session.Score == 0 && orchestrator.Session.Milestones == Phase2MilestoneFlags.None, "reset clears logic and state");
                Check(orchestrator.VisualHistoryCount == 0 && !orchestrator.InputEnabled, "reset clears history and input");
                readback = ReadMask(orchestrator.MaskRenderer.MaskTexture, readback);
                Check(MaximumRed(readback) == 0, "reset clears visual mask");

                orchestrator.Release();
                Check(!orchestrator.IsPrepared && orchestrator.MaskRenderer.OwnedRenderTextureCount == 0 && orchestrator.VisualHistoryCount == 0, "release clears GPU ownership and history");
                Debug.Log($"[Phase2Orchestration][Test] result={passed}/{total}, failures=0");
            }
            finally
            {
                if (readback)
                {
                    UnityEngine.Object.DestroyImmediate(readback);
                }
                orchestrator.Release();
            }
        }

        private static bool NoMutation(Phase2PaintOrchestrator orchestrator)
        {
            return orchestrator.Session.Grid.PaintedCellCount == 0 &&
                   orchestrator.Session.Score == 0 &&
                   orchestrator.Session.Milestones == Phase2MilestoneFlags.None &&
                   orchestrator.VisualHistoryCount == 0;
        }

        private static bool HistoryIsStrictlyOrdered(IReadOnlyList<Phase2PaintStamp> history, int startIndex)
        {
            if (history.Count - startIndex < 2)
            {
                return false;
            }
            for (int i = startIndex + 1; i < history.Count; i++)
            {
                if (history[i].CenterU <= history[i - 1].CenterU || Math.Abs(history[i].CenterV - history[i - 1].CenterV) > 0.000001f)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool PointsStrictlyAdvance(IReadOnlyList<(float u, float v)> points)
        {
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].u <= points[i - 1].u || !Approximately(points[i].v, points[i - 1].v))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Approximately(float a, float b)
        {
            return Math.Abs(a - b) <= 0.000001f;
        }

        private static Texture2D ReadMask(RenderTexture source, Texture2D existing)
        {
            if (!existing || existing.width != source.width || existing.height != source.height)
            {
                if (existing) UnityEngine.Object.DestroyImmediate(existing);
                existing = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
                {
                    name = "Phase2_OrchestrationValidationReadback",
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

        private static float SampleRed(Texture2D texture, float u, float v)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(u * (texture.width - 1)), 0, texture.width - 1);
            int y = Mathf.Clamp(Mathf.RoundToInt(v * (texture.height - 1)), 0, texture.height - 1);
            return texture.GetPixel(x, y).r;
        }

        private static byte MaximumRed(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            byte maximum = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r > maximum) maximum = pixels[i].r;
            }
            return maximum;
        }

        private static bool RedChannelsEqual(Color32[] expected, Color32[] actual)
        {
            if (expected.Length != actual.Length)
            {
                return false;
            }
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i].r != actual[i].r)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
#endif
