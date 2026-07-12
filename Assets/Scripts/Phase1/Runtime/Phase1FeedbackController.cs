using UnityEngine;

namespace HATAGONG.Phase1
{
    public sealed class Phase1FeedbackController : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Phase1GameConfig config;
        private bool reported;
        private void Awake(){ReportMissingOnce();}
        public void ReportMissingOnce(){if(reported||!config)return;reported=true;if(!config.NormalHitClip||!config.DamageStateChangeClip||!config.DestroyClip||!config.CoreHitClip)Debug.Log("[Phase1] One or more feedback AudioClip slots are empty; gameplay will continue silently for those slots.");}
        public void Play(Phase1TileRole role,Phase1TileGrade grade,bool stateChanged,bool destroyed)
        {
            if(!config)return; AudioClip clip;float volume;
            var visual=config.GetVisualSet(grade);
            if(destroyed){clip=visual!=null&&visual.DestroyAudioOverride?visual.DestroyAudioOverride:config.DestroyClip;volume=config.DestroyVolume;}
            else if(stateChanged){clip=visual!=null&&visual.DamageAudioOverride?visual.DamageAudioOverride:config.DamageStateChangeClip;volume=config.DamageStateChangeVolume;}
            else if(visual!=null&&visual.HitAudioOverride){clip=visual.HitAudioOverride;volume=config.NormalHitVolume;}
            else if(role==Phase1TileRole.Core){clip=config.CoreHitClip?config.CoreHitClip:config.NormalHitClip;volume=config.CoreHitClip?config.CoreHitVolume:config.NormalHitVolume;}
            else{clip=config.NormalHitClip;volume=config.NormalHitVolume;}
            if(config.EnableSound&&audioSource&&clip)audioSource.PlayOneShot(clip,volume);
            bool vibrate=config.EnableVibration&&(destroyed?config.VibrateOnDestroy:stateChanged?config.VibrateOnDamageStateChange:config.VibrateOnNormalHit);
            if(vibrate)VibrateSafely();
        }
        private static void VibrateSafely()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            try{Handheld.Vibrate();}catch(System.Exception e){Debug.LogWarning($"[Phase1] Vibration unavailable: {e.Message}");}
#endif
        }
    }
}
