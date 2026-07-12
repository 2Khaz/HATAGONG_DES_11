using System;
using TMPro;
using UnityEngine;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase1
{
    public sealed class Phase1ScoreController : MonoBehaviour
    {
        [SerializeField] private GameScoreController gameScore;
        public int Score => gameScore?gameScore.CurrentScore:0;
        public event Action<int> ScoreChanged;
        public void ResetScore(){gameScore?.ResetForNewSession();ScoreChanged?.Invoke(Score);}
        public void Add(int amount,ScoreReason reason=ScoreReason.Other){if(gameScore&&gameScore.AddScore(amount,GamePhaseId.Phase1,reason))ScoreChanged?.Invoke(Score);}
    }
}
