using System;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestSelectionController : MonoBehaviour
    {
        [SerializeField] private Button questIndicatorButton;
        [SerializeField] private GameObject requestPopupLayer;
        [SerializeField] private Button dimButton;
        [SerializeField] private OutgameRequestPopupView popupView;
        [SerializeField] private OutgameRequestCarouselController carouselController;

        public bool IsReady { get; private set; }
        public int BatchSeed { get; private set; }
        public int BatchSeedGenerationCount { get; private set; }
        public int LoadAttemptCount { get; private set; }
        public int SceneLoadRequestCount { get; private set; }
        public bool IsTransitionRequested { get; private set; }
        public OutgameRequestOfferBatch Batch { get; private set; }

        private bool hasReportedLoadFailure;

        private void Awake()
        {
            if (questIndicatorButton != null)
            {
                questIndicatorButton.interactable = false;
                questIndicatorButton.onClick.AddListener(OpenPopup);
            }
            if (dimButton != null) dimButton.onClick.AddListener(ClosePopup);
            if (carouselController != null) carouselController.PageChanged += HandlePageChanged;
            if (requestPopupLayer != null) requestPopupLayer.SetActive(false);
        }

        private async void Start()
        {
            try
            {
                LoadAttemptCount++;
                OutgameRequestTableLoadResult loaded = await OutgameRequestTableLoader.LoadAsync();
                if (!loaded.Success || loaded.Catalog == null)
                {
                    ReportInitializationFailure("CSV load failed", loaded.Errors);
                    return;
                }

                BatchSeed = CreateSessionBatchSeed();
                BatchSeedGenerationCount++;
                OutgameRequestOfferGenerationResult generated =
                    OutgameRequestOfferGenerator.Generate(loaded.Catalog, BatchSeed);
                if (!generated.Success || generated.Batch == null)
                {
                    ReportInitializationFailure("Offer generation failed", generated.Errors);
                    return;
                }

                Batch = generated.Batch;
                popupView.Bind(Batch);
                RegisterCardListeners();
                carouselController.Initialize(Batch.Offers.Count);
                SetAllPerformButtons(false);
                IsReady = true;
                questIndicatorButton.interactable = true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[Outgame][RequestSelection] Initialization failed once: " + exception);
            }
        }

        public void OpenPopup()
        {
            if (!IsReady || Batch == null || IsTransitionRequested) return;
            requestPopupLayer.SetActive(true);
            Canvas.ForceUpdateCanvases();
            carouselController.RefreshLayout();
            carouselController.ResetToFirstPage();
            UpdatePerformButtonsForCurrentPage();
        }

        public void ClosePopup()
        {
            SetAllPerformButtons(false);
            if (requestPopupLayer != null) requestPopupLayer.SetActive(false);
        }

        private void OnDestroy()
        {
            if (questIndicatorButton != null) questIndicatorButton.onClick.RemoveListener(OpenPopup);
            if (dimButton != null) dimButton.onClick.RemoveListener(ClosePopup);
            if (carouselController != null) carouselController.PageChanged -= HandlePageChanged;
            UnregisterCardListeners();
        }

        private void RegisterCardListeners()
        {
            UnregisterCardListeners();
            for (int i = 0; i < popupView.Cards.Count; i++)
                popupView.Cards[i].PerformRequested += HandlePerformRequested;
        }

        private void UnregisterCardListeners()
        {
            if (popupView == null) return;
            for (int i = 0; i < popupView.Cards.Count; i++)
            {
                OutgameRequestCardView card = popupView.Cards[i];
                if (card != null) card.PerformRequested -= HandlePerformRequested;
            }
        }

        private void HandlePageChanged(int pageIndex)
        {
            UpdatePerformButtonsForCurrentPage();
        }

        private void UpdatePerformButtonsForCurrentPage()
        {
            bool allowSelection = IsReady && !IsTransitionRequested &&
                requestPopupLayer != null && requestPopupLayer.activeInHierarchy;
            int currentPage = carouselController == null ? -1 : carouselController.CurrentPageIndex;
            for (int i = 0; i < popupView.Cards.Count; i++)
                popupView.Cards[i].SetPerformInteractable(allowSelection && i == currentPage);
        }

        private void SetAllPerformButtons(bool interactable)
        {
            if (popupView == null) return;
            for (int i = 0; i < popupView.Cards.Count; i++)
                popupView.Cards[i].SetPerformInteractable(interactable);
        }

        private void HandlePerformRequested(OutgameRequestOffer offer)
        {
            if (offer == null || IsTransitionRequested) return;

            IsTransitionRequested = true;
            OutgameRequestSelectionStore.SetPending(offer);
            SetAllPerformButtons(false);
            if (questIndicatorButton != null) questIndicatorButton.interactable = false;
            if (dimButton != null) dimButton.interactable = false;

            try
            {
                SceneLoadRequestCount++;
                AsyncOperation operation = SceneManager.LoadSceneAsync("INGAME", LoadSceneMode.Single);
                if (operation == null)
                    throw new InvalidOperationException("INGAME LoadSceneAsync did not start.");
            }
            catch (Exception exception)
            {
                RestoreAfterLoadFailure();
                if (!hasReportedLoadFailure)
                {
                    hasReportedLoadFailure = true;
                    Debug.LogError("[Outgame][RequestSelection] INGAME load failed once: " + exception);
                }
            }
        }

        private void RestoreAfterLoadFailure()
        {
            OutgameRequestSelectionStore.Clear();
            IsTransitionRequested = false;
            if (questIndicatorButton != null) questIndicatorButton.interactable = IsReady;
            if (dimButton != null) dimButton.interactable = true;
            UpdatePerformButtonsForCurrentPage();
        }

        private static int CreateSessionBatchSeed()
        {
            var bytes = new byte[sizeof(int)];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                generator.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }

        private static void ReportInitializationFailure<T>(string label, System.Collections.Generic.IReadOnlyList<T> errors)
        {
            string detail = errors == null || errors.Count == 0
                ? "No error details."
                : string.Join(" | ", System.Linq.Enumerable.Select(errors, value => value == null ? "<null>" : value.ToString()));
            Debug.LogError("[Outgame][RequestSelection] " + label + ": " + detail);
        }
    }
}
