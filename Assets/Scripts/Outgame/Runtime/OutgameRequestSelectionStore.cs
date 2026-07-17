using HATAGONG.GameFlow;
using UnityEngine;

namespace HATAGONG.Outgame
{
    public static class OutgameRequestSelectionStore
    {
        private static OutgameRequestRunSelection pending;
        private static OutgameRequestRunSelection active;

        public static bool HasPending => pending != null;
        public static bool HasActive => active != null;

        public static bool TryGetPending(out OutgameRequestRunSelection selection)
        {
            selection = pending;
            return selection != null;
        }

        public static void SetPending(OutgameRequestOffer offer)
        {
            pending = new OutgameRequestRunSelection(offer);
            active = null;
        }

        public static bool ActivatePending()
        {
            if (pending == null) return false;
            active = pending;
            pending = null;
            return true;
        }

        public static bool TryPrepareRetry(GameRunContext context)
        {
            if (!context.HasSelectedRequest) return true;
            if (!Matches(active, context)) return false;
            pending = active;
            active = null;
            return true;
        }

        public static void CancelPreparedRetry(GameRunContext context)
        {
            if (!Matches(pending, context) || active != null) return;
            active = pending;
            pending = null;
        }

        public static void Clear()
        {
            pending = null;
            active = null;
        }

        private static bool Matches(OutgameRequestRunSelection selection, GameRunContext context)
        {
            return selection != null && context.HasSelectedRequest && selection.OfferSnapshot != null &&
                selection.RequestId == context.RequestId && selection.Difficulty == context.Difficulty &&
                selection.RequestType == context.RequestType && selection.PermanentSeed == context.PermanentSeed &&
                selection.Phase1Seed == context.Phase1Seed && selection.Phase2Seed == context.Phase2Seed &&
                selection.Phase3Seed == context.Phase3Seed;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession()
        {
            Clear();
        }
    }
}
