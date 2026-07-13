#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase2.Editor
{
    public static class Phase2LogicCoreValidation
    {
        [MenuItem("Tools/HATAGONG/Phase2/Validate Logic Core")]
        public static void Validate()
        {
            RunBatchValidation();
        }

        public static void RunBatchValidation()
        {
            Debug.Log("[Phase2LogicCore][Test] validation started");
            int pass = 0;
            int total = 0;
            void Check(bool condition, string name)
            {
                total++;
                if (!condition)
                {
                    throw new InvalidOperationException("[Phase2LogicCore][Test] " + name);
                }
                pass++;
            }

            var config = Phase2PaintConfig.CreateProduction();
            Check(config.Width == 128 && config.Height == 128, "production grid");
            Check(config.TotalCellCount == 16384, "total cell count");
            Check(config.RequiredClearCells == 16221, "required clear cells");
            Check(Phase2PaintPresets.Easy.NormalRadiusRatio == 0.085d, "easy radius preset");
            Check(Phase2PaintPresets.Hard.NormalRadiusRatio == 0.065d, "hard radius preset");
            Check(config.IsValid(out _), "valid config");

            var grid = new Phase2PaintGrid(config);
            Check(grid.PaintedCellCount == 0, "grid initial painted count");
            Check(grid.TotalCellCount == 16384, "grid total count");
            grid.Reset();
            Check(grid.PaintedCellCount == 0, "grid reset");

            int painted = grid.ApplyStamp(0.5f, 0.5f, 0.1f, config);
            Check(painted > 0, "central stamp paints cells");
            Check(grid.PaintedCellCount == painted, "painted count tracks new cells");

            var duplicate = grid.ApplyStamp(0.5f, 0.5f, 0.1f, config);
            Check(duplicate == 0, "duplicate stamp no new cells");

            var session = new Phase2PaintSessionModel(config);
            Check(session.CurrentState == Phase2PaintSessionState.Ready, "session initial state");
            session.Start();
            Check(session.IsRunning, "session starts");

            var result = session.ApplyStamp(0.5f, 0.5f, 0.1f, true, true);
            Check(result.Accepted, "accepted stamp");
            Check(result.NewlyPaintedCellCount > 0, "stamp increased painted cells");
            Check(result.CoverageScoreDelta >= 0, "coverage score delta non-negative");
            Check(result.TotalScoreDelta >= 0, "total score delta non-negative");

            var clearConfig = new Phase2PaintConfig(128, 128, 0.99d, 0.085d, 0.075d, 0.065d, 0.4d, 500, 100, 150, 200, 500);
            var clearSession = new Phase2PaintSessionModel(clearConfig);
            clearSession.Start();
            int target = clearConfig.RequiredClearCells - 1;
            Phase2StampResult belowThresholdResult = default;
            for (int i = 0; i < target; i++)
            {
                int x = i % 128;
                int y = i / 128;
                double u = (x + 0.5d) / 128d;
                double v = (y + 0.5d) / 128d;
                belowThresholdResult = clearSession.ApplyStamp((float)u, (float)v, 0.0001f, true, true);
            }
            Check(clearSession.Grid.TotalCellCount == 16384, "clear session total cells");
            Check(clearConfig.RequiredClearCells == 16221, "clear session required cells");
            Check(belowThresholdResult.TotalPaintedCellCount == 16220, "clear threshold painted count below required");
            Check(belowThresholdResult.ClearThresholdReached == false, "clear threshold below required");
            Check(belowThresholdResult.StateBefore == Phase2PaintSessionState.Running && belowThresholdResult.StateAfter == Phase2PaintSessionState.Running, "state remains running below threshold");

            int thresholdIndex = target;
            int thresholdX = thresholdIndex % 128;
            int thresholdY = thresholdIndex / 128;
            float thresholdU = (float)((thresholdX + 0.5d) / 128d);
            float thresholdV = (float)((thresholdY + 0.5d) / 128d);
            var thresholdResult = clearSession.ApplyStamp(thresholdU, thresholdV, 0.0001f, true, true);
            Check(thresholdResult.TotalPaintedCellCount == 16221, "clear threshold painted count reached");
            Check(thresholdResult.ClearScoreDelta == clearConfig.ClearBonus, "clear bonus calculated in threshold result");
            Check(thresholdResult.ClearThresholdReached == true, "clear threshold reached");
            Check(thresholdResult.StateBefore == Phase2PaintSessionState.Running && thresholdResult.StateAfter == Phase2PaintSessionState.Completing, "state enters completing at threshold");

            var rejectedAfterThreshold = clearSession.ApplyStamp(0.99f, 0.99f, 0.0001f, true, true);
            Check(rejectedAfterThreshold.Accepted == false && rejectedAfterThreshold.RejectionReason == Phase2PaintMutationRejectionReason.AlreadyCompleting, "stamp rejected while completing");
            Check(rejectedAfterThreshold.TotalPaintedCellCount == 16221, "rejected stamp preserves painted count");
            Check(rejectedAfterThreshold.ClearThresholdReached == false, "clear threshold reported only once");
            Check(rejectedAfterThreshold.StateBefore == Phase2PaintSessionState.Completing && rejectedAfterThreshold.StateAfter == Phase2PaintSessionState.Completing, "rejected stamp preserves completing state");

            var interpolator = new Phase2StampInterpolator();
            var points = new List<(float u, float v)>();
            interpolator.Begin(0.1f, 0.1f, points);
            interpolator.AppendSegment(0.3f, 0.1f, 0.1f, points);
            Check(points.Count >= 2, "interpolator creates spacing points");

            Debug.Log($"[Phase2LogicCore][Test] result={pass}/{total}, failures=0");
        }
    }
}
#endif
