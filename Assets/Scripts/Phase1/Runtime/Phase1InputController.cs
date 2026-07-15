using UnityEngine;

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
        public bool TryBegin(int pointerId,Phase1TileView tile,Vector2 screenPosition,Camera eventCamera){if(!inputEnabled||!tile||tile.IsDestroyed)return false;if(activePointer.HasValue&&activePointer.Value!=pointerId)return false;activePointer=pointerId;if(boardController&&boardController.CanPlayTouchEffect&&touchEffectController)try{touchEffectController.Play(screenPosition,eventCamera);}catch(System.Exception exception){Debug.LogWarning($"[Phase1][TouchEffect] Feedback skipped without blocking gameplay: {exception.Message}",touchEffectController);}return boardController&&boardController.TryHit(tile);}
        public void End(int pointerId){if(activePointer==pointerId)activePointer=null;}
        public void ReleaseFor(Phase1TileView tile){activePointer=null;}
        private void OnDisable(){activePointer=null;}
        private void OnApplicationFocus(bool focused){if(!focused)activePointer=null;}
    }
}
