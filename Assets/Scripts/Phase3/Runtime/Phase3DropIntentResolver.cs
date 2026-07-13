using UnityEngine;

namespace HATAGONG.Phase3
{
    public enum Phase3DropIntent { Cancel, Field, Deck }

    public static class Phase3DropIntentResolver
    {
        public static Phase3DropIntent Resolve(Vector3 displayedWorldCentroid, RectTransform deckRoot, RectTransform fieldRoot)
        {
            if (deckRoot)
            {
                Vector2 deckLocal = deckRoot.InverseTransformPoint(displayedWorldCentroid);
                if (Phase3FieldCoordinateMapper.IsStrictInterior(deckRoot.rect, deckLocal)) return Phase3DropIntent.Deck;
            }
            Vector2 fieldLocal = fieldRoot.InverseTransformPoint(displayedWorldCentroid);
            return fieldRoot.rect.Contains(fieldLocal) ? Phase3DropIntent.Field : Phase3DropIntent.Cancel;
        }
    }
}
