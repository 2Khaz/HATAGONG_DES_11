using System;
using System.Collections.Generic;

namespace HATAGONG.GameFlow
{
    public sealed class GamePhaseRegistry
    {
        private readonly Dictionary<GamePhaseId, IGamePhase> _byId;
        private readonly List<IGamePhase> _phases;

        private GamePhaseRegistry(Dictionary<GamePhaseId, IGamePhase> byId, List<IGamePhase> phases)
        {
            _byId = byId;
            _phases = phases;
        }

        public IReadOnlyList<IGamePhase> Phases => _phases;

        public static bool TryCreate(IEnumerable<object> candidates, GamePhaseId initialPhaseId, out GamePhaseRegistry registry, out string error)
        {
            registry = null;
            error = null;
            if (candidates == null)
            {
                error = "Phase registration list is missing.";
                return false;
            }

            var byId = new Dictionary<GamePhaseId, IGamePhase>();
            var phases = new List<IGamePhase>();
            foreach (object candidate in candidates)
            {
                if (candidate == null)
                {
                    error = "Phase registration contains a null entry.";
                    return false;
                }
                if (!(candidate is IGamePhase phase))
                {
                    error = $"Registered object does not implement {nameof(IGamePhase)}: {candidate.GetType().FullName}.";
                    return false;
                }
                if (byId.ContainsKey(phase.PhaseId))
                {
                    error = $"Duplicate phase id: {phase.PhaseId}.";
                    return false;
                }
                byId.Add(phase.PhaseId, phase);
                phases.Add(phase);
            }

            if (!byId.ContainsKey(initialPhaseId))
            {
                error = $"Initial phase is not registered: {initialPhaseId}.";
                return false;
            }

            registry = new GamePhaseRegistry(byId, phases);
            return true;
        }

        public bool TryGet(GamePhaseId phaseId, out IGamePhase phase)
        {
            return _byId.TryGetValue(phaseId, out phase);
        }

        public void DisableAllInput()
        {
            for (int i = 0; i < _phases.Count; i++)
            {
                _phases[i].SetInputEnabled(false);
            }
        }
    }
}
