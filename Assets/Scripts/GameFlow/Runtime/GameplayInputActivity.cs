using System;
using UnityEngine;

namespace HATAGONG.GameFlow
{
    public interface IGameplayInputStatus
    {
        bool IsGameplayInputEnabled { get; }
    }

    public static class GameplayInputActivity
    {
        public static event Action<GamePhaseId> ValidGameplayInput;

        public static void NotifyValidGameplayInput(GamePhaseId phaseId)
        {
            ValidGameplayInput?.Invoke(phaseId);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewPlayerLoop()
        {
            ValidGameplayInput = null;
        }
    }
}
