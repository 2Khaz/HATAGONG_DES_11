#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.GameFlow.Editor
{
    public static class GameTimerValidation
    {
        [MenuItem("Tools/HATAGONG/Game Flow/Validate Timer")]
        public static void ValidateTimer()
        {
            int passed = 0, total = 0;
            void Check(bool condition, string name) { total++; if (!condition) throw new InvalidOperationException("[GameTimer][Test] " + name); passed++; }

            var timer = new GameCountdownTimer(90d); int expired = 0; timer.TimerExpired += () => expired++;
            Check(timer.RemainingSeconds == 90d && timer.DisplayedSeconds == 90 && timer.CurrentState == GameTimerState.Idle, "initial state");
            Check(GameCountdownTimer.FormatSeconds(90) == "90", "initial format");
            timer.StartTimer(); timer.Tick(0d); Check(timer.DisplayedSeconds == 90, "zero tick");
            int displayEvents=0;timer.DisplayedSecondChanged+=_=>displayEvents++;timer.Tick(0.01d); Check(timer.DisplayedSeconds == 90&&displayEvents==0, "ceil first frame without UI update");
            for (int i = 0; i < 10; i++) timer.Tick(0.1d); Check(Math.Abs(timer.RemainingSeconds - 88.99d) < 0.000001d, "accumulation");
            var stepped=new GameCountdownTimer(90d);var single=new GameCountdownTimer(90d);stepped.StartTimer();single.StartTimer();for(int i=0;i<10;i++)stepped.Tick(0.1d);single.Tick(1d);Check(Math.Abs(stepped.RemainingSeconds-single.RemainingSeconds)<0.000001d,"equivalent delta accumulation");
            timer.PauseTimer(); double paused = timer.RemainingSeconds; timer.Tick(5d); timer.PauseTimer(); Check(timer.RemainingSeconds == paused && timer.IsPaused, "pause idempotence");
            timer.ResumeTimer(); timer.Tick(1d); Check(timer.RemainingSeconds < paused && timer.IsRunning, "resume");
            timer.ResetTimer(); Check(timer.RemainingSeconds == 90d && timer.CurrentState == GameTimerState.Idle, "reset");
            timer.StartTimer(); timer.Tick(1000d); timer.Tick(1d); Check(timer.RemainingSeconds == 0d && timer.IsExpired && expired == 1, "single expiration");
            timer.StartTimer(); Check(timer.IsExpired, "expired start policy");
            timer.ResetTimer(); Check(timer.CurrentState == GameTimerState.Idle && timer.DisplayedSeconds == 90, "expired reset");

            var displays = new[] { (90d,90), (89.999d,90), (89.001d,90), (89d,89), (1.001d,2), (1d,1), (0.001d,1), (0d,0) };
            foreach (var item in displays) Check(GameCountdownTimer.ToDisplayedSeconds(item.Item1) == item.Item2, "ceil " + item.Item1);
            foreach (int seconds in new[] { 90,60,10,9,1,0 }) { string text=GameCountdownTimer.FormatSeconds(seconds);Check(text==seconds.ToString()&&!text.Contains(":")&&(seconds>=10||text.Length==1),"integer format "+seconds); }
            foreach (double invalid in new[] { 0d,-1d,double.NaN,double.PositiveInfinity,double.NegativeInfinity }) { var value = new GameCountdownTimer(invalid); Check(value.IsExpired && value.DisplayedSeconds == 0, "invalid duration"); }
            Debug.Log($"[GameTimer][Test] result={passed}/{total}, failures=0");
        }
    }
}
#endif
