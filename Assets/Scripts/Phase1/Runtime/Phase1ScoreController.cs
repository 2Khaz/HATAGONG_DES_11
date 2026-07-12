using System;
using TMPro;
using UnityEngine;

namespace HATAGONG.Phase1
{
    public sealed class Phase1ScoreController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI scoreValueText;
        public int Score { get; private set; }
        public event Action<int> ScoreChanged;
        public void ResetScore(){Score=0;Refresh();}
        public void Add(int amount){Score+=amount;Refresh();ScoreChanged?.Invoke(Score);}
        private void Refresh(){if(scoreValueText)scoreValueText.text=Score.ToString();}
    }
}
