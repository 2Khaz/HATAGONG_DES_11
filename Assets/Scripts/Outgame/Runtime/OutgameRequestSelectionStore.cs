using UnityEngine;

namespace HATAGONG.Outgame
{
    public static class OutgameRequestSelectionStore
    {
        private static OutgameRequestRunSelection pending;

        public static bool HasPending => pending != null;

        public static bool TryGetPending(out OutgameRequestRunSelection selection)
        {
            selection = pending;
            return selection != null;
        }

        public static void SetPending(OutgameRequestOffer offer)
        {
            pending = new OutgameRequestRunSelection(offer);
        }

        public static void Clear()
        {
            pending = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            Clear();
        }
    }
}
