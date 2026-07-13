using System;
using UnityEngine;

namespace HATAGONG.Phase3
{
    [Serializable]
    public sealed class Phase3RuntimeVisualSettings
    {
        public Color TargetColor = new Color(0.25f, 0.8f, 1f, 0.2f);
        public Color PlacedColor = new Color(0.25f, 0.9f, 0.45f, 0.9f);
        public Color LooseColor = new Color(1f, 0.75f, 0.2f, 0.9f);
        public Color DraggingColor = new Color(1f, 0.9f, 0.35f, 1f);
        public Color DeckColor = new Color(0.9f, 0.9f, 0.95f, 1f);
        public Color OutlineColor = new Color(0f, 0f, 0f, 0.65f);
        [Min(0f)] public float OutlineWidth = 2f;
        [Min(0f)] public float DeckSlotPadding = 12f;
        [Range(0.01f, 1f)] public float DeckIconMaximumScale = 1f;
        [Min(1f)] public float DragEmphasisScale = 1.04f;
    }
}
