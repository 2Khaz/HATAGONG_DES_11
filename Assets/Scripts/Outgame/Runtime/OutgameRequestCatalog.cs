using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestValidationError
    {
        public OutgameRequestValidationError(
            string fileKind,
            int rowNumber,
            string columnName,
            string reason,
            string actualValue)
        {
            FileKind = fileKind ?? string.Empty;
            RowNumber = rowNumber;
            ColumnName = columnName ?? string.Empty;
            Reason = reason ?? string.Empty;
            ActualValue = actualValue ?? string.Empty;
        }

        public string FileKind { get; }
        public int RowNumber { get; }
        public string ColumnName { get; }
        public string Reason { get; }
        public string ActualValue { get; }

        public override string ToString()
        {
            string location = RowNumber > 0 ? $"row {RowNumber}" : "file";
            if (!string.IsNullOrEmpty(ColumnName)) location += $", column {ColumnName}";
            return $"[{FileKind}] {location}: {Reason} (actual='{ActualValue}')";
        }
    }

    public sealed class OutgameRequestTableLoadResult
    {
        private OutgameRequestTableLoadResult(
            bool success,
            OutgameRequestCatalog catalog,
            IEnumerable<OutgameRequestValidationError> errors)
        {
            Success = success;
            Catalog = catalog;
            Errors = Array.AsReadOnly(
                errors == null
                    ? Array.Empty<OutgameRequestValidationError>()
                    : new List<OutgameRequestValidationError>(errors).ToArray());
        }

        public bool Success { get; }
        public OutgameRequestCatalog Catalog { get; }
        public IReadOnlyList<OutgameRequestValidationError> Errors { get; }

        public static OutgameRequestTableLoadResult Succeeded(OutgameRequestCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return new OutgameRequestTableLoadResult(true, catalog, null);
        }

        public static OutgameRequestTableLoadResult Failed(
            IEnumerable<OutgameRequestValidationError> errors)
        {
            return new OutgameRequestTableLoadResult(false, null, errors);
        }
    }

    public sealed class OutgameRequestCatalog
    {
        private readonly IReadOnlyList<OutgameRequestDefinition> requests;
        private readonly IReadOnlyList<OutgameRequestDefinition> enabledRequests;
        private readonly IReadOnlyList<OutgameRequestEffectDefinition> effects;
        private readonly IReadOnlyDictionary<string, OutgameRequestDefinition> requestsById;
        private readonly IReadOnlyDictionary<string, OutgameRequestEffectDefinition> effectsById;

        internal OutgameRequestCatalog(
            IEnumerable<OutgameRequestDefinition> requests,
            IEnumerable<OutgameRequestEffectDefinition> effects)
        {
            var requestArray = new List<OutgameRequestDefinition>(requests).ToArray();
            var effectArray = new List<OutgameRequestEffectDefinition>(effects).ToArray();
            this.requests = Array.AsReadOnly(requestArray);
            enabledRequests = Array.AsReadOnly(requestArray.Where(value => value.Enabled).ToArray());
            this.effects = Array.AsReadOnly(effectArray);
            requestsById = new ReadOnlyDictionary<string, OutgameRequestDefinition>(
                requestArray.ToDictionary(value => value.RequestId, StringComparer.Ordinal));
            effectsById = new ReadOnlyDictionary<string, OutgameRequestEffectDefinition>(
                effectArray.ToDictionary(value => value.EffectId, StringComparer.Ordinal));
        }

        public IReadOnlyList<OutgameRequestDefinition> Requests => requests;
        public IReadOnlyList<OutgameRequestDefinition> EnabledRequests => enabledRequests;
        public IReadOnlyList<OutgameRequestEffectDefinition> Effects => effects;

        public bool TryGetRequest(string requestId, out OutgameRequestDefinition definition)
        {
            return requestsById.TryGetValue(requestId ?? string.Empty, out definition);
        }

        public bool TryGetEffect(string effectId, out OutgameRequestEffectDefinition definition)
        {
            return effectsById.TryGetValue(effectId ?? string.Empty, out definition);
        }

        public static OutgameRequestTableLoadResult LoadFromCsv(
            string requestsCsv,
            string requestEffectsCsv)
        {
            return OutgameRequestCsvParser.ParseAndValidate(requestsCsv, requestEffectsCsv);
        }
    }
}
