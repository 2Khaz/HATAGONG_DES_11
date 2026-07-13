using System;
using System.Collections;
using TMPro;
using UnityEngine;
namespace HATAGONG.GameFlow
{
    public sealed class PhaseTransitionOverlay:MonoBehaviour
    {
        [SerializeField]private CanvasGroup canvasGroup;[SerializeField]private RectTransform banner;[SerializeField]private TextMeshProUGUI messageText;
        [SerializeField]private float enterDuration=.2f,holdDuration=.25f,exitDuration=.2f,completionDelay=.1f;private Coroutine sequence;private Action<PhaseTransitionResult,bool,Exception> finished;private bool midpointSucceeded;
        public bool IsPlaying=>sequence!=null;public bool MidpointSucceeded=>midpointSucceeded;
        public float EnterDuration=>enterDuration;public float HoldDuration=>holdDuration;public float ExitDuration=>exitDuration;public float CompletionDelay=>completionDelay;
        public float TotalConfiguredDuration=>enterDuration+holdDuration+exitDuration+completionDelay;
        public bool Play(GamePhaseId phase,Func<bool> midpoint,Action<PhaseTransitionResult,bool,Exception> completion){if(sequence!=null)return false;gameObject.SetActive(true);finished=completion;midpointSucceeded=false;sequence=StartCoroutine(Run(phase,midpoint));return true;}
        private IEnumerator Run(GamePhaseId phase,Func<bool> midpoint){canvasGroup.alpha=1;canvasGroup.blocksRaycasts=true;messageText.text=$"PHASE {(int)phase} CLEAR!!";float width=((RectTransform)transform).rect.width;float distance=(width+banner.rect.width)*.5f;banner.anchoredPosition=new Vector2(distance,0);yield return Move(distance,0,enterDuration,false);midpointSucceeded=TryExecuteMidpoint(midpoint,out var failure);if(!midpointSucceeded){Finish(PhaseTransitionResult.Failed,failure??new InvalidOperationException("Midpoint reported failure."));yield break;}yield return Wait(holdDuration);yield return Move(0,-distance,exitDuration,true);yield return Wait(completionDelay);Finish(PhaseTransitionResult.Succeeded,null);}
        private static bool TryExecuteMidpoint(Func<bool> midpoint,out Exception failure){failure=null;try{return midpoint==null||midpoint();}catch(Exception e){failure=e;return false;}}
        private void Finish(PhaseTransitionResult result,Exception error){if(sequence==null)return;sequence=null;var callback=finished;finished=null;if(canvasGroup)canvasGroup.blocksRaycasts=false;gameObject.SetActive(false);callback?.Invoke(result,midpointSucceeded,error);}
        private void OnDisable(){if(sequence==null)return;StopCoroutine(sequence);sequence=null;var callback=finished;finished=null;if(canvasGroup)canvasGroup.blocksRaycasts=false;callback?.Invoke(PhaseTransitionResult.Interrupted,midpointSucceeded,null);}
        private IEnumerator Move(float from,float to,float duration,bool easeIn){float elapsed=0;while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(elapsed/Mathf.Max(.001f,duration));float eased=easeIn?t*t*t:1-Mathf.Pow(1-t,3);banner.anchoredPosition=new Vector2(Mathf.LerpUnclamped(from,to,eased),0);yield return null;}banner.anchoredPosition=new Vector2(to,0);}
        private static IEnumerator Wait(float duration){float elapsed=0;while(elapsed<duration){elapsed+=Time.unscaledDeltaTime;yield return null;}}
    }
}
