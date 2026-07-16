using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Phase1
{
    public sealed class Phase1HUDPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI difficultyText;
        [SerializeField] private Image[] difficultyStars=new Image[3];
        [SerializeField] private Sprite filledStarSprite,emptyStarSprite;
        public void Present(Phase1Difficulty difficulty){if(difficultyText)difficultyText.text=GetDifficultyLabel(difficulty);int count=(int)difficulty+1;for(int i=0;i<difficultyStars.Length;i++){var star=difficultyStars[i];if(!star)continue;star.gameObject.SetActive(true);star.enabled=true;var color=star.color;color.a=1;star.color=color;if(filledStarSprite&&emptyStarSprite)star.sprite=i<count?filledStarSprite:emptyStarSprite;}Debug.Log($"[Phase1][DifficultyUI] difficulty={difficulty}, filled={count}, stars={DescribeStars()}");}
        private static string GetDifficultyLabel(Phase1Difficulty difficulty)=>difficulty switch{Phase1Difficulty.Easy=>"쉬움",Phase1Difficulty.Normal=>"보통",Phase1Difficulty.Hard=>"어려움",_=>string.Empty};
        public string DescribeStars(){return string.Join(";",System.Array.ConvertAll(difficultyStars,s=>s?$"{s.name}:active={s.gameObject.activeSelf},enabled={s.enabled},sprite={(s.sprite?s.sprite.name:"null")},color={s.color},pos={s.rectTransform.anchoredPosition}":"null"));}
    }
}
