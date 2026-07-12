using UnityEngine;

namespace HATAGONG.Phase1
{
    public sealed class Phase1InputController : MonoBehaviour
    {
        [SerializeField] private Phase1BoardController boardController;
        private int? activePointer;
        public bool TryBegin(int pointerId,Phase1TileView tile){if(!tile||tile.IsDestroyed)return false;if(activePointer.HasValue&&activePointer.Value!=pointerId)return false;activePointer=pointerId;return boardController&&boardController.TryHit(tile);}
        public void End(int pointerId){if(activePointer==pointerId)activePointer=null;}
        public void ReleaseFor(Phase1TileView tile){activePointer=null;}
        private void OnDisable(){activePointer=null;}
        private void OnApplicationFocus(bool focused){if(!focused)activePointer=null;}
    }
}
