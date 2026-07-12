using System;
namespace HATAGONG.GameFlow { public interface IGamePhase { bool IsRunning{get;} bool IsCleared{get;} event Action PhaseCleared; void StartPhase(); void StopPhase(); void SetInputEnabled(bool enabled); } }
