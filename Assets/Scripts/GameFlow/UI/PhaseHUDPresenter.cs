using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HATAGONG.GameFlow
{
    public sealed class PhaseHUDPresenter:MonoBehaviour
    {
        [SerializeField]private TextMeshProUGUI phaseText,phaseDescription;[SerializeField]private Image[] dots=new Image[3];[SerializeField]private string[] descriptions={"철 거","도 포","시 공"};
        [SerializeField]private Sprite activeDotSprite,inactiveDotSprite;
        public GamePhaseId CurrentPhase{get;private set;}=GamePhaseId.Phase1;
        public void SetPhase(GamePhaseId phase){CurrentPhase=phase;int selected=(int)phase-1;if(phaseText)phaseText.text=$"PHASE {(int)phase}";if(phaseDescription)phaseDescription.text=descriptions!=null&&selected>=0&&selected<descriptions.Length?descriptions[selected]:string.Empty;for(int i=0;i<dots.Length;i++){var dot=dots[i];if(!dot)continue;dot.gameObject.SetActive(true);dot.enabled=true;Sprite sprite=i==selected?activeDotSprite:inactiveDotSprite;if(sprite)dot.sprite=sprite;dot.color=Color.white;}}
    }
}
