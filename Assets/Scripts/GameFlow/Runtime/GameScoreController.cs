using System;
using TMPro;
using UnityEngine;
namespace HATAGONG.GameFlow
{
    public sealed class GameScoreController:MonoBehaviour
    {
        [SerializeField]private TextMeshProUGUI scoreValueText;
        [SerializeField]private GameSessionController session;
        public int CurrentScore{get;private set;}
        public bool IsLocked{get;private set;}
        public event Action<int> ScoreChanged;
        public bool AddScore(int amount,GamePhaseId phase=GamePhaseId.Phase1,ScoreReason reason=ScoreReason.Other)
        {if(amount<=0||IsLocked||(session&&!session.CanAddScore))return false;CurrentScore+=amount;Refresh();ScoreChanged?.Invoke(CurrentScore);Debug.Log($"[GameScore] phase={phase}, reason={reason}, amount={amount}, total={CurrentScore}");return true;}
        public void ResetForNewSession(){CurrentScore=0;IsLocked=false;Refresh();ScoreChanged?.Invoke(CurrentScore);}
        public void LockScore(){IsLocked=true;}
        private void Refresh(){if(scoreValueText)scoreValueText.text=CurrentScore.ToString();}
    }
}
