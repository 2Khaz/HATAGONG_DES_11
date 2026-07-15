using System;
using System.Collections.Generic;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestOfferGenerationResult
    {
        private readonly IReadOnlyList<string> errors;

        private OutgameRequestOfferGenerationResult(
            bool success,
            OutgameRequestOfferBatch batch,
            IEnumerable<string> errors)
        {
            Success = success;
            Batch = batch;
            var copy = errors == null
                ? Array.Empty<string>()
                : new List<string>(errors).ToArray();
            this.errors = Array.AsReadOnly(copy);
        }

        public bool Success { get; }
        public OutgameRequestOfferBatch Batch { get; }
        public IReadOnlyList<string> Errors => errors;

        internal static OutgameRequestOfferGenerationResult Succeeded(
            OutgameRequestOfferBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            return new OutgameRequestOfferGenerationResult(true, batch, null);
        }

        internal static OutgameRequestOfferGenerationResult Failed(
            IEnumerable<string> errors)
        {
            var copy = errors == null ? new List<string>() : new List<string>(errors);
            if (copy.Count == 0) copy.Add("Offer generation failed.");
            return new OutgameRequestOfferGenerationResult(false, null, copy);
        }
    }
}
