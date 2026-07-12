using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HATAGONG.Phase1
{
    public sealed class Phase1TileView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image hitArea;
        [SerializeField] private Image visualImage;
        [SerializeField] private RectTransform visualRoot;
        [SerializeField] private int tileId,gridX,gridY,gridWidth,gridHeight,baseHp,gradeHpModifier,maxHp,currentHp;
        [SerializeField] private Phase1TileShape shape; [SerializeField] private Phase1TileRole role; [SerializeField] private Phase1DamageState damageState;
        [SerializeField] private Phase1TileGrade grade;[SerializeField] private string gradeId,visualSetId,usedSpriteName;[SerializeField] private bool visualFallbackUsed,minimumHpValid,isDestroyed;
        private Phase1InputController input;private Phase1GameConfig config;private float lastHitTime=-999;private Coroutine punch;
        public int TileId=>tileId; public int CurrentHp=>currentHp; public int MaxHp=>maxHp; public int BaseHp=>baseHp;public int GradeHpModifier=>gradeHpModifier;public Phase1TileGrade Grade=>grade; public Phase1TileShape Shape=>shape; public Phase1TileRole Role=>role; public Phase1DamageState DamageState=>damageState; public bool IsDestroyed=>isDestroyed;
        public void Initialize(Phase1TilePlacement p,Phase1InputController input,Phase1GameConfig config){tileId=p.TileId;gridX=p.GridX;gridY=p.GridY;gridWidth=p.GridWidth;gridHeight=p.GridHeight;baseHp=p.BaseHp;gradeHpModifier=p.GradeHpModifier;maxHp=p.MaxHp;currentHp=maxHp;shape=p.Shape;role=p.Role;grade=p.Grade;gradeId=p.GradeId;visualSetId=p.VisualSetId;minimumHpValid=p.MinimumHpValid;damageState=Phase1DamageState.Normal;isDestroyed=false;this.input=input;this.config=config;if(!hitArea)hitArea=GetComponent<Image>();if(hitArea)hitArea.raycastTarget=true;ConfigureOrientation();RefreshSprite();}
        public bool CanHit()=>!isDestroyed&&Time.unscaledTime-lastHitTime>=config.TileDebounce;
        public void DebugAllowImmediateHit(){lastHitTime=-999f;}
        public bool ApplyDamage(out bool stateChanged,out bool destroyed){stateChanged=false;destroyed=false;if(!CanHit())return false;lastHitTime=Time.unscaledTime;var old=damageState;currentHp=Mathf.Max(0,currentHp-1);damageState=CalculateState(maxHp,currentHp);stateChanged=old!=damageState;destroyed=currentHp==0;RefreshSprite();if(!destroyed)Punch();return true;}
        public void DestroyVisual(){if(isDestroyed)return;isDestroyed=true;damageState=Phase1DamageState.Destroyed;if(hitArea)hitArea.raycastTarget=false;if(visualRoot)visualRoot.gameObject.SetActive(false);input?.ReleaseFor(this);}
        public void OnPointerDown(PointerEventData e){input?.TryBegin(e.pointerId,this);} public void OnPointerUp(PointerEventData e){input?.End(e.pointerId);}
        private void OnDisable(){input?.ReleaseFor(this);}
        private void RefreshSprite(){if(!visualImage||!config)return;var sprite=config.GetSprite(grade,shape,damageState,out bool fallback);visualFallbackUsed=fallback;usedSpriteName=sprite?sprite.name:string.Empty;if(sprite){visualImage.sprite=sprite;visualImage.color=Color.white;}else{visualImage.sprite=null;visualImage.color=config.GetFallbackColor(grade);}}
        private void ConfigureOrientation(){if(!visualRoot)return;bool vertical=gridHeight>gridWidth&&gridWidth!=gridHeight;if(vertical){visualRoot.anchorMin=visualRoot.anchorMax=new Vector2(.5f,.5f);visualRoot.pivot=new Vector2(.5f,.5f);visualRoot.anchoredPosition=Vector2.zero;visualRoot.sizeDelta=new Vector2(((RectTransform)transform).rect.height,((RectTransform)transform).rect.width);visualRoot.localRotation=Quaternion.Euler(0,0,90);}else{visualRoot.anchorMin=Vector2.zero;visualRoot.anchorMax=Vector2.one;visualRoot.offsetMin=visualRoot.offsetMax=Vector2.zero;visualRoot.localRotation=Quaternion.identity;}}
        private void Punch(){if(!visualRoot||!config)return;if(punch!=null)StopCoroutine(punch);visualRoot.localScale=Vector3.one;punch=StartCoroutine(PunchRoutine());}
        private IEnumerator PunchRoutine(){yield return ScaleTo(config.PunchScale,config.PunchDownDuration);yield return ScaleTo(1,config.PunchReturnDuration);visualRoot.localScale=Vector3.one;punch=null;}
        private IEnumerator ScaleTo(float target,float duration){Vector3 start=visualRoot.localScale,end=Vector3.one*target;for(float t=0;t<duration;t+=Time.unscaledDeltaTime){visualRoot.localScale=Vector3.Lerp(start,end,duration<=0?1:t/duration);yield return null;}visualRoot.localScale=end;}
        public static Phase1DamageState CalculateState(int max,int current){if(current<=0)return Phase1DamageState.Destroyed;if(max==2)return current==2?Phase1DamageState.Normal:Phase1DamageState.Damage2;float r=(float)current/max;if(max<=5)return r>.75f?Phase1DamageState.Normal:r>.5f?Phase1DamageState.Damage1:Phase1DamageState.Damage3;return r>.75f?Phase1DamageState.Normal:r>.5f?Phase1DamageState.Damage1:r>.25f?Phase1DamageState.Damage2:Phase1DamageState.Damage3;}
    }
}
