using HATAGONG.GameFlow;
using UnityEngine;

namespace HATAGONG.Phase1
{
    public sealed class Phase1FeedbackController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Phase1GameConfig config;
        private void Awake(){GameSfxPlayer.EnsureInstance();ReportMissingOnce();}
        public void ReportMissingOnce(){}
        public void Play(Phase1TileRole role,Phase1TileGrade grade,bool stateChanged,bool destroyed,bool directImpact)
        {
            if(!config)return;
            if(directImpact)
            {
                bool powerHit=IngameItemSystemController.Instance&&IngameItemSystemController.Instance.ActiveItem==GameItemId.Hammer;
                GameSfxPlayer.Play(powerHit?GameSfxId.PowerHit:GameSfxId.Hit);
            }
            if(destroyed)GameSfxPlayer.Play(GameSfxId.TileDestroy);
            else if(stateChanged)GameSfxPlayer.Play(GameSfxId.TileCrack);
            bool vibrate=config.EnableVibration&&(destroyed?config.VibrateOnDestroy:stateChanged?config.VibrateOnDamageStateChange:config.VibrateOnNormalHit);
            if(vibrate&&IngameOptionPreferences.VibrationEnabled)VibrateSafely();
        }
        private static void VibrateSafely()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            try{Handheld.Vibrate();}catch(System.Exception e){Debug.LogWarning($"[Phase1] Vibration unavailable: {e.Message}");}
#endif
        }
    }
}
