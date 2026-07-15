using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestPopupView : MonoBehaviour
    {
        [SerializeField] private RectTransform requestContent;
        [SerializeField] private OutgameRequestCardView cardPrefab;

        private readonly List<OutgameRequestCardView> cards = new List<OutgameRequestCardView>();
        private ReadOnlyCollection<OutgameRequestCardView> readOnlyCards;

        public IReadOnlyList<OutgameRequestCardView> Cards =>
            readOnlyCards ?? (readOnlyCards = cards.AsReadOnly());

        public void Bind(OutgameRequestOfferBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (batch.Offers.Count < 1 || batch.Offers.Count > OutgameRequestOfferGenerator.MaximumOfferCount)
                throw new ArgumentOutOfRangeException(nameof(batch), "A popup batch must contain one to three offers.");
            if (requestContent == null || cardPrefab == null)
                throw new InvalidOperationException("Popup View references are not configured.");

            ClearCards();
            for (int i = 0; i < batch.Offers.Count; i++)
            {
                OutgameRequestCardView card = Instantiate(cardPrefab, requestContent, false);
                card.name = "OutgameRequestCard_" + (i + 1);
                card.gameObject.SetActive(true);
                card.Bind(batch.Offers[i]);
                cards.Add(card);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(requestContent);
        }

        public void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                OutgameRequestCardView card = cards[i];
                if (card == null) continue;
                card.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(card.gameObject);
                else DestroyImmediate(card.gameObject);
            }
            cards.Clear();
        }
    }
}
