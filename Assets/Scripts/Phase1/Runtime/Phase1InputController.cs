using UnityEngine;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase1
{
    public sealed class Phase1InputController : MonoBehaviour
    {
        [SerializeField] private Phase1BoardController boardController;
        [SerializeField] private Phase1TouchEffectController touchEffectController;
        [SerializeField] private bool inputEnabled=true;
        private int? activePointer;
        public bool InputEnabled=>inputEnabled;
        private void Awake(){if(!touchEffectController)touchEffectController=GetComponentInChildren<Phase1TouchEffectController>(true);if(!touchEffectController){Transform layer=transform.Find("Phase1_EffectRoot");if(layer)touchEffectController=layer.gameObject.AddComponent<Phase1TouchEffectController>();}}
        public void SetInputEnabled(bool enabled){inputEnabled=enabled;if(!enabled)activePointer=null;}
        public bool TryBegin(int pointerId,Phase1TileView tile){if(!inputEnabled||!tile||tile.IsDestroyed)return false;if(activePointer.HasValue&&activePointer.Value!=pointerId)return false;activePointer=pointerId;return boardController&&boardController.TryHit(tile);}
        public bool TryBegin(int pointerId,Phase1TileView tile,Vector2 screenPosition,Camera eventCamera){if(!inputEnabled||!tile||tile.IsDestroyed)return false;if(activePointer.HasValue&&activePointer.Value!=pointerId)return false;activePointer=pointerId;if(boardController&&boardController.CanPlayTouchEffect&&touchEffectController)try{Color color=IngameItemSystemController.Instance&&IngameItemSystemController.Instance.ActiveItem==GameItemId.Hammer?new Color(1f,0.72f,0.08f,1f):Color.white;touchEffectController.Play(screenPosition,eventCamera,color);}catch(System.Exception exception){Debug.LogWarning($"[Phase1][TouchEffect] Feedback skipped without blocking gameplay: {exception.Message}",touchEffectController);}return boardController&&boardController.TryHit(tile);}
        public void PlayItemHitFeedback(Vector3 worldPosition){if(!touchEffectController)return;try{touchEffectController.PlayAtWorldPosition(worldPosition,Color.white);}catch(System.Exception exception){Debug.LogWarning($"[Phase1][TouchEffect] Item feedback skipped without blocking gameplay: {exception.Message}",touchEffectController);}}
        public void End(int pointerId){if(activePointer==pointerId)activePointer=null;}
        public void ReleaseFor(Phase1TileView tile){activePointer=null;}
        private void OnDisable(){activePointer=null;}
        private void OnApplicationFocus(bool focused){if(!focused)activePointer=null;}
    }
}
