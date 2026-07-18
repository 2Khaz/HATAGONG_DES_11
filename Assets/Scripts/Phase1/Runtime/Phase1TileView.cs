using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private int tileId,gridX,gridY,gridWidth,gridHeight,baseHp,gradeHpModifier,balanceHpModifier,requestHpModifier,maxHp,currentHp;
        [SerializeField] private Phase1TileShape shape; [SerializeField] private Phase1TileRole role; [SerializeField] private Phase1DamageState damageState;
        [SerializeField] private Phase1TileGrade grade;[SerializeField] private string gradeId,visualSetId,usedSpriteName;[SerializeField] private bool visualFallbackUsed,minimumHpValid,isSuperTile,isDestroyed;
        private Phase1InputController input;private Phase1GameConfig config;private Phase1TilePlacement placement;private float lastHitTime=-999;private Coroutine punch;
        public int TileId=>tileId; public int CurrentHp=>currentHp; public int MaxHp=>maxHp; public int BaseHp=>baseHp;public int GradeHpModifier=>gradeHpModifier;public int BalanceHpModifier=>balanceHpModifier;public int RequestHpModifier=>requestHpModifier;public bool IsSuperTile=>isSuperTile;public Phase1TileGrade Grade=>grade; public Phase1TileShape Shape=>shape; public Phase1TileRole Role=>role; public Phase1DamageState DamageState=>damageState; public bool IsDestroyed=>isDestroyed; public bool CanReceiveDamage=>CanHit();
        public Vector3 WorldCenter{get{RectTransform rect=transform as RectTransform;return rect?rect.TransformPoint(rect.rect.center):transform.position;}}
        public void Initialize(Phase1TilePlacement p,Phase1InputController input,Phase1GameConfig config){placement=p;tileId=p.TileId;gridX=p.GridX;gridY=p.GridY;gridWidth=p.GridWidth;gridHeight=p.GridHeight;baseHp=p.BaseHp;gradeHpModifier=p.GradeHpModifier;balanceHpModifier=p.BalanceHpModifier;requestHpModifier=p.RequestHpModifier;maxHp=p.MaxHp;currentHp=maxHp;shape=p.Shape;role=p.Role;grade=p.Grade;gradeId=p.GradeId;visualSetId=p.VisualSetId;minimumHpValid=p.MinimumHpValid;isSuperTile=p.IsSuperTile;damageState=Phase1DamageState.Normal;isDestroyed=false;this.input=input;this.config=config;if(!hitArea)hitArea=GetComponent<Image>();if(hitArea)hitArea.raycastTarget=true;ConfigureOrientation();RefreshSprite();}
        public bool CanHit()=>!isDestroyed&&Time.unscaledTime-lastHitTime>=config.TileDebounce;
        public void DebugAllowImmediateHit(){lastHitTime=-999f;}
        public bool ApplyDamage(out bool stateChanged,out bool destroyed)=>ApplyDamage(1,out stateChanged,out destroyed);
        public bool ApplyDamage(int damage,out bool stateChanged,out bool destroyed){stateChanged=false;destroyed=false;if(damage<=0||!CanHit())return false;lastHitTime=Time.unscaledTime;var old=damageState;currentHp=Mathf.Max(0,currentHp-damage);damageState=CalculateState(maxHp,currentHp);stateChanged=old!=damageState;destroyed=currentHp==0;RefreshSprite();if(!destroyed)Punch();return true;}
        public void DestroyVisual(){if(isDestroyed)return;isDestroyed=true;damageState=Phase1DamageState.Destroyed;if(hitArea)hitArea.raycastTarget=false;if(visualRoot)visualRoot.gameObject.SetActive(false);input?.ReleaseFor(this);}
        public void OnPointerDown(PointerEventData e){input?.TryBegin(e.pointerId,this,e.position,e.pressEventCamera);} public void OnPointerUp(PointerEventData e){input?.End(e.pointerId);}
        private void OnDisable(){input?.ReleaseFor(this);}
        private void RefreshSprite(){if(!visualImage||!config)return;var sprite=Phase1TileVisualResources.GetBlockSprite(shape,damageState);bool superFallback=false;var material=isSuperTile?Phase1TileVisualResources.GetSuperMaterial(grade,out superFallback):Phase1TileVisualResources.GetMaterial(grade);visualFallbackUsed=superFallback||!sprite||!material;usedSpriteName=sprite?sprite.name:string.Empty;if(placement!=null){placement.VisualFallbackUsed=visualFallbackUsed;placement.UsedSpriteName=usedSpriteName;}if(sprite){visualImage.sprite=sprite;visualImage.material=material;visualImage.color=Color.white;}else{visualImage.sprite=null;visualImage.material=null;visualImage.color=config.GetFallbackColor(grade);}}
        private void ConfigureOrientation(){if(!visualRoot)return;bool vertical=gridHeight>gridWidth&&gridWidth!=gridHeight;if(vertical){visualRoot.anchorMin=visualRoot.anchorMax=new Vector2(.5f,.5f);visualRoot.pivot=new Vector2(.5f,.5f);visualRoot.anchoredPosition=Vector2.zero;visualRoot.sizeDelta=new Vector2(((RectTransform)transform).rect.height,((RectTransform)transform).rect.width);visualRoot.localRotation=Quaternion.Euler(0,0,90);}else{visualRoot.anchorMin=Vector2.zero;visualRoot.anchorMax=Vector2.one;visualRoot.offsetMin=visualRoot.offsetMax=Vector2.zero;visualRoot.localRotation=Quaternion.identity;}}
        private void Punch(){if(!visualRoot||!config)return;if(punch!=null)StopCoroutine(punch);visualRoot.localScale=Vector3.one;punch=StartCoroutine(PunchRoutine());}
        private IEnumerator PunchRoutine(){yield return ScaleTo(config.PunchScale,config.PunchDownDuration);yield return ScaleTo(1,config.PunchReturnDuration);visualRoot.localScale=Vector3.one;punch=null;}
        private IEnumerator ScaleTo(float target,float duration){Vector3 start=visualRoot.localScale,end=Vector3.one*target;for(float t=0;t<duration;t+=Time.unscaledDeltaTime){visualRoot.localScale=Vector3.Lerp(start,end,duration<=0?1:t/duration);yield return null;}visualRoot.localScale=end;}
        public static Phase1DamageState CalculateState(int max,int current){if(current<=0)return Phase1DamageState.Destroyed;if(max==2)return current==2?Phase1DamageState.Normal:Phase1DamageState.Damage2;float r=(float)current/max;if(max<=5)return r>.75f?Phase1DamageState.Normal:r>.5f?Phase1DamageState.Damage1:Phase1DamageState.Damage3;return r>.75f?Phase1DamageState.Normal:r>.5f?Phase1DamageState.Damage1:r>.25f?Phase1DamageState.Damage2:Phase1DamageState.Damage3;}
    }

    public static class Phase1TileVisualResources
    {
        private const string BlockRoot="Ingame/Block/",TextureRoot=BlockRoot+"Texture/",SuperTexturePath=TextureRoot+"Tile_SUPER",ShaderName="HATAGONG/Phase1/UI Tile Composite";
        private static readonly Dictionary<string,Sprite> sprites=new();
        private static readonly Dictionary<Phase1TileGrade,Material> materials=new();
        private static Material superMaterial;
        private static bool superTextureMissing;
        private static bool warnedMissingSuperVisual;
        public static Sprite GetBlockSprite(Phase1TileShape shape,Phase1DamageState state)
        {
            if(state==Phase1DamageState.Destroyed)state=Phase1DamageState.Damage3;
            string path=BlockRoot+"Img_"+SizeName(shape)+"tiles_"+StageNumber(state);if(sprites.TryGetValue(path,out var cached))return cached;
            var texture=Resources.Load<Texture2D>(path);if(!texture){Debug.LogError("[Phase1][Visual] Missing block texture: "+path);return null;}
            var sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);sprite.name=texture.name;sprite.hideFlags=HideFlags.DontSave;sprites.Add(path,sprite);return sprite;
        }
        public static Material GetSuperMaterial(Phase1TileGrade fallbackGrade,out bool usedFallback)
        {
            if(superMaterial){usedFallback=false;return superMaterial;}
            if(!superTextureMissing){var shader=Shader.Find(ShaderName);var texture=Resources.Load<Texture2D>(SuperTexturePath);if(shader&&texture){superMaterial=new Material(shader){name="Phase1Tile_SUPER",hideFlags=HideFlags.DontSave};superMaterial.SetTexture("_MaterialTex",texture);usedFallback=false;return superMaterial;}superTextureMissing=true;}
            usedFallback=true;if(!warnedMissingSuperVisual){warnedMissingSuperVisual=true;Debug.LogWarning("[Phase1][RequestEffect] Tile_SUPER texture is not available; the tile's existing material is used as fallback.");}return GetMaterial(fallbackGrade);
        }
        public static Material GetMaterial(Phase1TileGrade grade)
        {
            if(materials.TryGetValue(grade,out var cached))return cached;var shader=Shader.Find(ShaderName);var texture=Resources.Load<Texture2D>(TextureRoot+TextureName(grade));
            if(!shader||!texture){Debug.LogError($"[Phase1][Visual] Missing composite resource: shader={shader}, grade={grade}, texture={texture}");return null;}
            var material=new Material(shader){name="Phase1Tile_"+grade,hideFlags=HideFlags.DontSave};material.SetTexture("_MaterialTex",texture);materials.Add(grade,material);return material;
        }
        public static bool ValidateAllResources(out string error)
        {
            foreach(Phase1TileShape shape in System.Enum.GetValues(typeof(Phase1TileShape)))foreach(Phase1DamageState state in new[]{Phase1DamageState.Normal,Phase1DamageState.Damage1,Phase1DamageState.Damage2,Phase1DamageState.Damage3})if(!GetBlockSprite(shape,state)){error=$"Missing block sprite: {shape}/{state}";return false;}
            foreach(Phase1TileGrade grade in System.Enum.GetValues(typeof(Phase1TileGrade)))if(!GetMaterial(grade)){error="Missing material texture: "+grade;return false;}error=string.Empty;return true;
        }
        private static string SizeName(Phase1TileShape shape)=>shape switch{Phase1TileShape.OneByOne=>"1×1",Phase1TileShape.OneByTwo=>"1×2",Phase1TileShape.OneByThree=>"1×3",Phase1TileShape.TwoByTwo=>"2×2",Phase1TileShape.TwoByThree=>"2×3",Phase1TileShape.ThreeByThree=>"3×3",_=>throw new System.ArgumentOutOfRangeException(nameof(shape),shape,null)};
        private static string StageNumber(Phase1DamageState state)=>state switch{Phase1DamageState.Normal=>"01",Phase1DamageState.Damage1=>"02",Phase1DamageState.Damage2=>"03",Phase1DamageState.Damage3=>"04",_=>"04"};
        private static string TextureName(Phase1TileGrade grade)=>grade switch{Phase1TileGrade.Beige=>"Tile_Beige",Phase1TileGrade.Brown=>"Tile_Orange",Phase1TileGrade.Gray=>"Tile_Gray",Phase1TileGrade.Marble=>"Tile_Marble",_=>throw new System.ArgumentOutOfRangeException(nameof(grade),grade,null)};
    }
}
