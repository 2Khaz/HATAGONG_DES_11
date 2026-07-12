using System;
using UnityEngine;

namespace HATAGONG.Phase1
{
    [Serializable]
    public sealed class Phase1TileGradeVisualSet
    {
        [SerializeField] private Phase1TileGrade grade;
        [SerializeField] private string visualSetId;
        [SerializeField] private Color fallbackColor;
        [SerializeField] private Sprite squareNormalSprite,squareDamage1Sprite,squareDamage2Sprite,squareDamage3Sprite;
        [SerializeField] private Sprite rect2x1NormalSprite,rect2x1Damage1Sprite,rect2x1Damage2Sprite,rect2x1Damage3Sprite;
        [SerializeField] private Sprite rect3x1NormalSprite,rect3x1Damage1Sprite,rect3x1Damage2Sprite,rect3x1Damage3Sprite;
        [SerializeField] private Sprite rect3x2NormalSprite,rect3x2Damage1Sprite,rect3x2Damage2Sprite,rect3x2Damage3Sprite;
        [SerializeField] private AudioClip hitAudioOverride,damageAudioOverride,destroyAudioOverride;
        public Phase1TileGrade Grade=>grade; public string VisualSetId=>visualSetId; public Color FallbackColor=>fallbackColor; public AudioClip HitAudioOverride=>hitAudioOverride; public AudioClip DamageAudioOverride=>damageAudioOverride; public AudioClip DestroyAudioOverride=>destroyAudioOverride;
        public Phase1TileGradeVisualSet(Phase1TileGrade grade){this.grade=grade;visualSetId=grade.ToString().ToUpperInvariant();fallbackColor=DefaultColor(grade);}
        public void EnsureFallbackColor(){if(fallbackColor.a<=0f)fallbackColor=DefaultColor(grade);}
        private static Color DefaultColor(Phase1TileGrade value)=>value switch
        {
            Phase1TileGrade.Beige=>new Color(0.86f,0.76f,0.59f,1f),
            Phase1TileGrade.Brown=>new Color(0.48f,0.29f,0.16f,1f),
            Phase1TileGrade.Gray=>new Color(0.48f,0.51f,0.54f,1f),
            Phase1TileGrade.Marble=>new Color(0.88f,0.90f,0.92f,1f),
            _=>Color.white
        };
        public Sprite Get(Phase1TileShape shape,Phase1DamageState state,out bool fallback)
        {
            Sprite normal,d1,d2,d3;
            if(shape==Phase1TileShape.OneByOne||shape==Phase1TileShape.TwoByTwo||shape==Phase1TileShape.ThreeByThree){normal=squareNormalSprite;d1=squareDamage1Sprite;d2=squareDamage2Sprite;d3=squareDamage3Sprite;}
            else if(shape==Phase1TileShape.OneByTwo){normal=rect2x1NormalSprite;d1=rect2x1Damage1Sprite;d2=rect2x1Damage2Sprite;d3=rect2x1Damage3Sprite;}
            else if(shape==Phase1TileShape.OneByThree){normal=rect3x1NormalSprite;d1=rect3x1Damage1Sprite;d2=rect3x1Damage2Sprite;d3=rect3x1Damage3Sprite;}
            else {normal=rect3x2NormalSprite;d1=rect3x2Damage1Sprite;d2=rect3x2Damage2Sprite;d3=rect3x2Damage3Sprite;}
            var selected=state==Phase1DamageState.Damage1?d1:state==Phase1DamageState.Damage2?d2:state==Phase1DamageState.Damage3?d3:normal;fallback=!selected;return selected?selected:normal;
        }
    }
}
