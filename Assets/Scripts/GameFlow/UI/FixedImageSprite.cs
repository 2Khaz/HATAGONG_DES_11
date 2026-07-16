using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class FixedImageSprite : MonoBehaviour
    {
        [SerializeField] private Image target;
        [SerializeField] private Sprite fixedSprite;

        private void Awake() => Apply();
        private void OnEnable() => Apply();
        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (target && fixedSprite && target.sprite != fixedSprite)
            {
                target.sprite = fixedSprite;
            }
        }
    }
}
