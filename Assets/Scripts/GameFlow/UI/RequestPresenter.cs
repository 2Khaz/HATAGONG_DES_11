using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace HATAGONG.GameFlow
{
    public sealed class RequestPresenter:MonoBehaviour
    {
        [SerializeField]private GameRequestContext context;[SerializeField]private TextMeshProUGUI requestText;[SerializeField]private Image requestIcon;
        [SerializeField]private string normalText="NORMAL REQUEST",suddenText="SUDDEN REQUEST";[SerializeField]private Sprite normalIcon,suddenIcon;
        private bool missingIconReported;public Sprite NormalIcon=>normalIcon;public Sprite SuddenIcon=>suddenIcon;public Sprite CurrentIcon=>requestIcon?requestIcon.sprite:null;
        private void OnEnable(){if(context)context.RequestChanged+=Present;Present(context?context.CurrentRequestType:RequestType.Normal);}private void OnDisable(){if(context)context.RequestChanged-=Present;}
        public void Present(RequestType type){if(requestText)requestText.text=type==RequestType.Normal?normalText:suddenText;if(!requestIcon)return;var sprite=type==RequestType.Normal?normalIcon:suddenIcon;if(!sprite){if(!missingIconReported){missingIconReported=true;Debug.LogError($"[GameFlow][Request] Missing {type} icon reference.",this);}return;}requestIcon.sprite=sprite;requestIcon.color=Color.white;}
    }
}
