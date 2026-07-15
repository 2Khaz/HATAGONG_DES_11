using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Phase1
{
    [RequireComponent(typeof(RectMask2D))]
    public sealed class Phase1TouchEffectController : MonoBehaviour
    {
        private const int PoolSize = 6;
        private static readonly Vector2 EffectSize = new Vector2(180f, 184f);
        private readonly List<Phase1TouchEffectView> pool = new List<Phase1TouchEffectView>(PoolSize);
        private readonly Sprite[] frames = new Sprite[3];
        private RectTransform layer;
        private long playOrder;

        public int PoolCount => pool.Count;

        private void Awake()
        {
            layer = (RectTransform)transform;
            frames[0] = Resources.Load<Sprite>("Ingame/Effect/Img_touch1");
            frames[1] = Resources.Load<Sprite>("Ingame/Effect/Img_touch2");
            frames[2] = Resources.Load<Sprite>("Ingame/Effect/Img_touch3");
            if (!frames[0] || !frames[1] || !frames[2])
            {
                Debug.LogError("[Phase1][TouchEffect] Required Resources/Ingame/Effect/Img_touch1~3 sprites are missing.", this);
                enabled = false;
                return;
            }
            BuildPool();
        }

        public bool Play(Vector2 screenPosition, Camera eventCamera)
        {
            if (!isActiveAndEnabled || pool.Count != PoolSize) return false;
            if (!RectTransformUtility.RectangleContainsScreenPoint(layer, screenPosition, eventCamera)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPosition, eventCamera, out Vector2 localPosition)) return false;

            Phase1TouchEffectView view = SelectView();
            view.transform.SetAsLastSibling();
            view.Play(localPosition, frames, ++playOrder);
            return true;
        }

        private void OnDisable()
        {
            for (int i = 0; i < pool.Count; i++) if (pool[i]) pool[i].StopAndHide();
        }

        private void BuildPool()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"Phase1_TouchEffect_{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Phase1TouchEffectView));
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(layer, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = EffectSize;
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                Image image = go.GetComponent<Image>();
                Phase1TouchEffectView view = go.GetComponent<Phase1TouchEffectView>();
                view.Initialize(image);
                pool.Add(view);
            }
        }

        private Phase1TouchEffectView SelectView()
        {
            Phase1TouchEffectView oldest = pool[0];
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].IsPlaying) return pool[i];
                if (pool[i].StartedOrder < oldest.StartedOrder) oldest = pool[i];
            }
            return oldest;
        }
    }
}
