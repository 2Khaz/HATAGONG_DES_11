using System;
using HATAGONG.Phase1;
using UnityEngine;
namespace HATAGONG.GameFlow
{
    public sealed class Phase1PhaseAdapter:MonoBehaviour,IGamePhase
    {
        [SerializeField]private Phase1BoardController board;
        [SerializeField]private Phase1InputController input;
        public bool IsRunning{get;private set;}public bool IsCleared{get;private set;}
        public event Action PhaseCleared;
        private void OnEnable(){if(board)board.Phase1Cleared+=OnCleared;}private void OnDisable(){if(board)board.Phase1Cleared-=OnCleared;}
        private void OnCleared(Phase1BoardState _){IsCleared=true;IsRunning=false;PhaseCleared?.Invoke();}
        public void StartPhase(){IsRunning=true;IsCleared=false;}public void StopPhase(){IsRunning=false;SetInputEnabled(false);}public void SetInputEnabled(bool enabled){if(input)input.SetInputEnabled(enabled);}
    }
}
