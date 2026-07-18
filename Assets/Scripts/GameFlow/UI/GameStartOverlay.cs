using System;
using System.Collections;
using TMPro;
using UnityEngine;
namespace HATAGONG.GameFlow
{
    public sealed class GameStartOverlay:MonoBehaviour
    {
        [SerializeField]private CanvasGroup canvasGroup;[SerializeField]private TextMeshProUGUI messageText;
        [SerializeField]private float readyDuration=.85f,readyToGoGap=.3f,goDuration=.45f,fadeDuration=.2f;private Coroutine sequence;
        public bool IsPlaying=>sequence!=null;public float ReadyDuration=>readyDuration;public float ReadyToGoGap=>readyToGoGap;public float GoDuration=>goDuration;public float FadeDuration=>fadeDuration;public string CurrentMessage=>messageText?messageText.text:string.Empty;
        public bool Play(Action completed){if(sequence!=null)return false;gameObject.SetActive(true);sequence=StartCoroutine(Run(completed));return true;}
        private IEnumerator Run(Action completed){canvasGroup.alpha=1;canvasGroup.blocksRaycasts=true;messageText.text="READY...";Debug.Log("[GameFlow][StartOverlay] READY...");yield return Wait(readyDuration);messageText.text=string.Empty;Debug.Log("[GameFlow][StartOverlay] gap");yield return Wait(readyToGoGap);messageText.text="GO!!";Debug.Log("[GameFlow][StartOverlay] GO!!");yield return Wait(goDuration);float elapsed=0;while(elapsed<fadeDuration){elapsed+=Time.unscaledDeltaTime;canvasGroup.alpha=1-Mathf.Clamp01(elapsed/Mathf.Max(.001f,fadeDuration));yield return null;}canvasGroup.blocksRaycasts=false;sequence=null;gameObject.SetActive(false);Debug.Log("[GameFlow][StartOverlay] completed");completed?.Invoke();}
        private void OnDisable(){if(sequence==null)return;StopCoroutine(sequence);sequence=null;if(canvasGroup)canvasGroup.blocksRaycasts=false;}
        private static IEnumerator Wait(float duration){float elapsed=0;while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;yield return null;}}
    }
}
