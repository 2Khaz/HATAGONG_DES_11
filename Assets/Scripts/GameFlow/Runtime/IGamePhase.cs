using System;

namespace HATAGONG.GameFlow
{
    public interface IGamePhase
    {
        GamePhaseId PhaseId { get; }
        bool IsPrepared { get; }
        bool IsRunning { get; }
        bool IsCleared { get; }
        bool IsExitReady { get; }

        event Action PhaseCleared;
        event Action PhaseExitReady;

        bool Prepare(GameRunContext context);
        bool Activate();
        void Deactivate();
        void SetInputEnabled(bool enabled);
    }
}
