using System;
using HATAGONG.Phase1;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.GameFlow
{
    public sealed class Phase1PhaseAdapter : MonoBehaviour, IGamePhase
    {
        [SerializeField] private Phase1BoardController board;
        [SerializeField] private Phase1InputController input;
        [SerializeField] private Image deckPanel;
        [SerializeField] private Sprite deckSprite;

        private bool _exitReadyRaised;
        private Phase1BoardController _subscribedBoard;

        public GamePhaseId PhaseId => GamePhaseId.Phase1;
        public bool IsPrepared { get; private set; }
        public bool IsRunning { get; private set; }
        public bool IsCleared { get; private set; }
        public bool IsExitReady { get; private set; }

        public event Action PhaseCleared;
        public event Action PhaseExitReady;

        private void OnEnable()
        {
            EnsureSubscribed();
        }

        private void OnDisable()
        {
            SetInputEnabled(false);
            IsRunning = false;
            UnsubscribeBoardEvents();
        }

        public bool Prepare(GameRunContext context)
        {
            SetInputEnabled(false);
            if (!ApplyDeckSprite() || !TryConvertDifficulty(context.Difficulty, out Phase1Difficulty phase1Difficulty) || !board)
            {
                return false;
            }
            if (!EnsureSubscribed()) return false;
            if (IsPrepared)
            {
                return board.Prepare(phase1Difficulty);
            }

            IsCleared = false;
            IsExitReady = false;
            IsRunning = false;
            _exitReadyRaised = false;
            IsPrepared = board.Prepare(phase1Difficulty);
            return IsPrepared;
        }

        public bool Activate()
        {
            SetInputEnabled(false);
            if (!ApplyDeckSprite() || !IsPrepared || !board || !board.IsPrepared || !EnsureSubscribed())
            {
                return false;
            }
            gameObject.SetActive(true);
            IsRunning = true;
            return true;
        }

        public void Deactivate()
        {
            SetInputEnabled(false);
            IsRunning = false;
            UnsubscribeBoardEvents();
            gameObject.SetActive(false);
        }

        public void SetInputEnabled(bool enabled)
        {
            if (input) input.SetInputEnabled(enabled && IsPrepared && IsRunning && !IsExitReady);
        }

        public static bool TryConvertDifficulty(GameDifficulty difficulty, out Phase1Difficulty phase1Difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    phase1Difficulty = Phase1Difficulty.Easy;
                    return true;
                case GameDifficulty.Normal:
                    phase1Difficulty = Phase1Difficulty.Normal;
                    return true;
                case GameDifficulty.Hard:
                    phase1Difficulty = Phase1Difficulty.Hard;
                    return true;
                default:
                    phase1Difficulty = default;
                    return false;
            }
        }

        private void OnClearConditionReached()
        {
            if (IsCleared) return;
            SetInputEnabled(false);
            IsCleared = true;
            PhaseCleared?.Invoke();
        }

        private void OnFinalized(Phase1BoardState _)
        {
            if (!IsCleared) OnClearConditionReached();
            IsRunning = false;

            // Phase1BoardController.Clear raises the existing Phase1Cleared event only
            // after clear score settlement and the final Phase 1 completion log.
            if (_exitReadyRaised) return;
            _exitReadyRaised = true;
            IsExitReady = true;
            PhaseExitReady?.Invoke();
        }

        private bool EnsureSubscribed()
        {
            if (!board) return false;
            if (ReferenceEquals(_subscribedBoard, board)) return true;

            UnsubscribeBoardEvents();
            board.Phase1ClearConditionReached += OnClearConditionReached;
            board.Phase1Cleared += OnFinalized;
            _subscribedBoard = board;
            return true;
        }

        private void UnsubscribeBoardEvents()
        {
            if (!_subscribedBoard) return;
            _subscribedBoard.Phase1ClearConditionReached -= OnClearConditionReached;
            _subscribedBoard.Phase1Cleared -= OnFinalized;
            _subscribedBoard = null;
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
