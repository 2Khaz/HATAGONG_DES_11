using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HATAGONG.GameFlow;

namespace HATAGONG.Outgame
{
    public static class OutgameRequestCsvParser
    {
        public static readonly IReadOnlyList<string> RequestHeaders = Array.AsReadOnly(new[]
        {
            "RequestId", "Enabled", "RequestType", "Difficulty", "PermanentSeed",
            "Phase1Seed", "Phase3Seed", "Phase3ImageKey", "RequesterName", "PortraitKey",
            "Title", "Description", "Effect1Id", "Effect2Id", "Effect3Id"
        });

        private static readonly HashSet<string> AllowedPhase3ImageKeys = new HashSet<string>(StringComparer.Ordinal)
        {
            "Img_bigtiles1", "Img_bigtiles2", "Img_bigtiles3"
        };

        public static readonly IReadOnlyList<string> EffectHeaders = Array.AsReadOnly(new[]
        {
            "EffectId", "Enabled", "EffectName", "EffectIconKey", "EffectScriptKey", "Description"
        });

        public static OutgameRequestTableLoadResult ParseAndValidate(
            string requestsCsv,
            string requestEffectsCsv)
        {
            var errors = new List<OutgameRequestValidationError>();
            CsvDocument requestDocument = ParseDocument(
                requestsCsv, "requests.csv", RequestHeaders, errors);
            CsvDocument effectDocument = ParseDocument(
                requestEffectsCsv, "request_effects.csv", EffectHeaders, errors);

            var rawEffects = ParseEffects(effectDocument, errors);
            var rawRequests = ParseRequests(requestDocument, errors);
            ValidateUniqueEffects(rawEffects, errors);
            for (int i = 0; i < RequestEffectRuntime.SupportedEffectIds.Count; i++)
            {
                string expectedId = RequestEffectRuntime.SupportedEffectIds[i];
                if (!rawEffects.Any(effect => string.Equals(effect.EffectId, expectedId, StringComparison.Ordinal)))
                    errors.Add(Error("request_effects.csv", 0, "EffectId", "Required runtime EffectId is missing.", expectedId));
            }
            ValidateRequests(rawRequests, rawEffects, errors);

            if (errors.Count > 0)
            {
                return OutgameRequestTableLoadResult.Failed(errors);
            }

            var effectDefinitions = new List<OutgameRequestEffectDefinition>(rawEffects.Count);
            var effectsById = new Dictionary<string, OutgameRequestEffectDefinition>(StringComparer.Ordinal);
            for (int i = 0; i < rawEffects.Count; i++)
            {
                RawEffect raw = rawEffects[i];
                var definition = new OutgameRequestEffectDefinition(
                    raw.EffectId, raw.Enabled, raw.EffectName, raw.EffectIconKey, raw.EffectScriptKey, raw.Description);
                effectDefinitions.Add(definition);
                effectsById.Add(definition.EffectId, definition);
            }

            var requestDefinitions = new List<OutgameRequestDefinition>(rawRequests.Count);
            for (int i = 0; i < rawRequests.Count; i++)
            {
                RawRequest raw = rawRequests[i];
                var requestEffects = new List<OutgameRequestEffectDefinition>(raw.EffectIds.Count);
                for (int effectIndex = 0; effectIndex < raw.EffectIds.Count; effectIndex++)
                {
                    requestEffects.Add(effectsById[raw.EffectIds[effectIndex]]);
                }
                requestDefinitions.Add(new OutgameRequestDefinition(
                    raw.RequestId,
                    raw.Enabled,
                    raw.RequestType,
                    raw.Difficulty,
                    raw.PermanentSeed,
                    raw.Phase1Seed,
                    raw.Phase3Seed,
                    raw.Phase3ImageKey,
                    raw.RequesterName,
                    raw.PortraitKey,
                    raw.Title,
                    raw.Description,
                    requestEffects));
            }

            return OutgameRequestTableLoadResult.Succeeded(
                new OutgameRequestCatalog(requestDefinitions, effectDefinitions));
        }

        private static CsvDocument ParseDocument(
            string content,
            string fileKind,
            IReadOnlyList<string> expectedHeaders,
            List<OutgameRequestValidationError> errors)
        {
            if (content == null)
            {
                errors.Add(Error(fileKind, 0, string.Empty, "CSV content is null.", "<null>"));
                return CsvDocument.Invalid;
            }

            if (content.Length > 0 && content[0] == '\uFEFF') content = content.Substring(1);
            List<CsvRow> rows = ParseRows(content, fileKind, errors);
            if (rows.Count == 0)
            {
                errors.Add(Error(fileKind, 1, string.Empty, "CSV header is missing.", string.Empty));
                return CsvDocument.Invalid;
            }

            CsvRow header = rows[0];
            bool headerValid = true;
            if (header.Fields.Count != expectedHeaders.Count)
            {
                errors.Add(Error(
                    fileKind,
                    header.RowNumber,
                    string.Empty,
                    $"Header column count must be {expectedHeaders.Count}.",
                    header.Fields.Count.ToString(CultureInfo.InvariantCulture)));
                headerValid = false;
            }

            int comparable = Math.Min(header.Fields.Count, expectedHeaders.Count);
            for (int i = 0; i < comparable; i++)
            {
                if (string.Equals(header.Fields[i], expectedHeaders[i], StringComparison.Ordinal)) continue;
                errors.Add(Error(
                    fileKind,
                    header.RowNumber,
                    expectedHeaders[i],
                    $"Header at position {i + 1} must be '{expectedHeaders[i]}'.",
                    header.Fields[i]));
                headerValid = false;
            }

            var dataRows = new List<CsvRow>();
            for (int i = 1; i < rows.Count; i++)
            {
                CsvRow row = rows[i];
                if (row.Fields.Count != expectedHeaders.Count)
                {
                    errors.Add(Error(
                        fileKind,
                        row.RowNumber,
                        string.Empty,
                        $"Row must contain exactly {expectedHeaders.Count} columns.",
                        row.Fields.Count.ToString(CultureInfo.InvariantCulture)));
                    continue;
                }
                dataRows.Add(row);
            }
            return new CsvDocument(headerValid, dataRows);
        }

        private static List<CsvRow> ParseRows(
            string content,
            string fileKind,
            List<OutgameRequestValidationError> errors)
        {
            var rows = new List<CsvRow>();
            var fields = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            bool quoteClosed = false;
            bool fieldStarted = false;
            bool recordStarted = false;
            int line = 1;
            int recordLine = 1;

            for (int index = 0; index < content.Length; index++)
            {
                char current = content[index];
                if (inQuotes)
                {
                    if (current == '"')
                    {
                        if (index + 1 < content.Length && content[index + 1] == '"')
                        {
                            field.Append('"');
                            index++;
                        }
                        else
                        {
                            inQuotes = false;
                            quoteClosed = true;
                        }
                    }
                    else if (current == '\r' || current == '\n')
                    {
                        if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n') index++;
                        field.Append('\n');
                        line++;
                    }
                    else
                    {
                        field.Append(current);
                    }
                    recordStarted = true;
                    continue;
                }

                if (quoteClosed)
                {
                    if (current == ',')
                    {
                        fields.Add(field.ToString());
                        field.Clear();
                        quoteClosed = false;
                        fieldStarted = false;
                        recordStarted = true;
                        continue;
                    }
                    if (current == '\r' || current == '\n')
                    {
                        fields.Add(field.ToString());
                        AddRow(rows, fields, recordLine);
                        field.Clear();
                        quoteClosed = false;
                        fieldStarted = false;
                        recordStarted = false;
                        if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n') index++;
                        line++;
                        recordLine = line;
                        continue;
                    }

                    errors.Add(Error(
                        fileKind,
                        line,
                        string.Empty,
                        "Only a comma or line ending may follow a closing quote.",
                        current.ToString()));
                    quoteClosed = false;
                    field.Append(current);
                    recordStarted = true;
                    continue;
                }

                if (current == '"')
                {
                    if (fieldStarted || field.Length > 0)
                    {
                        errors.Add(Error(
                            fileKind,
                            line,
                            string.Empty,
                            "A quote may only begin an empty field.",
                            field.ToString() + current));
                        field.Append(current);
                    }
                    else
                    {
                        inQuotes = true;
                        fieldStarted = true;
                    }
                    recordStarted = true;
                    continue;
                }

                if (current == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    fieldStarted = false;
                    recordStarted = true;
                    continue;
                }

                if (current == '\r' || current == '\n')
                {
                    fields.Add(field.ToString());
                    AddRow(rows, fields, recordLine);
                    field.Clear();
                    fieldStarted = false;
                    recordStarted = false;
                    if (current == '\r' && index + 1 < content.Length && content[index + 1] == '\n') index++;
                    line++;
                    recordLine = line;
                    continue;
                }

                field.Append(current);
                fieldStarted = true;
                recordStarted = true;
            }

            if (inQuotes)
            {
                errors.Add(Error(
                    fileKind,
                    recordLine,
                    string.Empty,
                    "Quoted field is not closed.",
                    field.ToString()));
            }
            if (recordStarted || fieldStarted || quoteClosed || fields.Count > 0)
            {
                fields.Add(field.ToString());
                AddRow(rows, fields, recordLine);
            }
            return rows;
        }

        private static void AddRow(List<CsvRow> rows, List<string> fields, int rowNumber)
        {
            rows.Add(new CsvRow(rowNumber, fields.ToArray()));
            fields.Clear();
        }

        private static List<RawEffect> ParseEffects(
            CsvDocument document,
            List<OutgameRequestValidationError> errors)
        {
            var result = new List<RawEffect>();
            if (!document.HeaderValid) return result;
            for (int i = 0; i < document.Rows.Count; i++)
            {
                CsvRow row = document.Rows[i];
                string effectId = NormalizeId(row[0]);
                Require(row, 0, "EffectId", effectId, "request_effects.csv", errors);
                bool enabled = ParseBoolean(row, 1, "Enabled", "request_effects.csv", errors);
                string effectName = row[2];
                string effectIconKey = row[3];
                string effectScriptKey = NormalizeId(row[4]);
                string description = ConvertDescription(row[5]);
                Require(row, 2, "EffectName", effectName, "request_effects.csv", errors);
                Require(row, 3, "EffectIconKey", effectIconKey, "request_effects.csv", errors);
                Require(row, 4, "EffectScriptKey", effectScriptKey, "request_effects.csv", errors);
                Require(row, 5, "Description", description, "request_effects.csv", errors);
                if (RequestEffectRuntime.TryGetContract(effectId, out string expectedIcon, out string expectedScript))
                {
                    if (!string.Equals(effectIconKey, expectedIcon, StringComparison.Ordinal))
                        errors.Add(Error("request_effects.csv", row.RowNumber, "EffectIconKey", $"EffectIconKey must be '{expectedIcon}' for {effectId}.", effectIconKey));
                    if (!string.Equals(effectScriptKey, expectedScript, StringComparison.Ordinal))
                        errors.Add(Error("request_effects.csv", row.RowNumber, "EffectScriptKey", $"EffectScriptKey must be '{expectedScript}' for {effectId}.", effectScriptKey));
                }
                else
                {
                    errors.Add(Error("request_effects.csv", row.RowNumber, "EffectId", "EffectId has no runtime contract.", effectId));
                }
                result.Add(new RawEffect(
                    row.RowNumber, effectId, enabled, effectName, effectIconKey, effectScriptKey, description));
            }
            return result;
        }

        private static List<RawRequest> ParseRequests(
            CsvDocument document,
            List<OutgameRequestValidationError> errors)
        {
            var result = new List<RawRequest>();
            if (!document.HeaderValid) return result;
            for (int i = 0; i < document.Rows.Count; i++)
            {
                CsvRow row = document.Rows[i];
                string requestId = NormalizeId(row[0]);
                Require(row, 0, "RequestId", requestId, "requests.csv", errors);
                bool enabled = ParseBoolean(row, 1, "Enabled", "requests.csv", errors);
                RequestType requestType = ParseRequestType(row, 2, errors);
                GameDifficulty difficulty = ParseDifficulty(row, 3, errors);
                int permanentSeed = ParseSeed(row, 4, "PermanentSeed", errors);
                int phase1Seed = ParseSeed(row, 5, "Phase1Seed", errors);
                int phase3Seed = ParseSeed(row, 6, "Phase3Seed", errors);
                string phase3ImageKey = NormalizeId(row[7]);
                string requesterName = row[8];
                string portraitKey = row[9];
                string title = row[10];
                string description = ConvertDescription(row[11]);
                Require(row, 7, "Phase3ImageKey", phase3ImageKey, "requests.csv", errors);
                if (!string.IsNullOrEmpty(phase3ImageKey) && !AllowedPhase3ImageKeys.Contains(phase3ImageKey))
                {
                    errors.Add(Error(
                        "requests.csv", row.RowNumber, "Phase3ImageKey",
                        "Phase3ImageKey must be Img_bigtiles1, Img_bigtiles2, or Img_bigtiles3.", phase3ImageKey));
                }
                Require(row, 8, "RequesterName", requesterName, "requests.csv", errors);
                Require(row, 9, "PortraitKey", portraitKey, "requests.csv", errors);
                Require(row, 10, "Title", title, "requests.csv", errors);
                Require(row, 11, "Description", description, "requests.csv", errors);

                string effect1 = NormalizeId(row[12]);
                string effect2 = NormalizeId(row[13]);
                string effect3 = NormalizeId(row[14]);
                if (string.IsNullOrEmpty(effect1) &&
                    (!string.IsNullOrEmpty(effect2) || !string.IsNullOrEmpty(effect3)))
                {
                    errors.Add(Error(
                        "requests.csv", row.RowNumber, "Effect1Id",
                        "Effect slots must be left-aligned without gaps.", effect1));
                }
                if (string.IsNullOrEmpty(effect2) && !string.IsNullOrEmpty(effect3))
                {
                    errors.Add(Error(
                        "requests.csv", row.RowNumber, "Effect2Id",
                        "Effect slots must be left-aligned without gaps.", effect2));
                }

                var effectIds = new List<string>(3);
                if (!string.IsNullOrEmpty(effect1)) effectIds.Add(effect1);
                if (!string.IsNullOrEmpty(effect2)) effectIds.Add(effect2);
                if (!string.IsNullOrEmpty(effect3)) effectIds.Add(effect3);
                if (effectIds.Count < 0 || effectIds.Count > 3)
                {
                    errors.Add(Error(
                        "requests.csv", row.RowNumber, "Effect1Id",
                        "Effect count must be between 0 and 3.",
                        effectIds.Count.ToString(CultureInfo.InvariantCulture)));
                }
                string duplicateEffect = effectIds
                    .GroupBy(value => value, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(duplicateEffect))
                {
                    errors.Add(Error(
                        "requests.csv", row.RowNumber, "Effect1Id",
                        "A request cannot reference the same EffectId more than once.", duplicateEffect));
                }

                result.Add(new RawRequest(
                    row.RowNumber,
                    requestId,
                    enabled,
                    requestType,
                    difficulty,
                    permanentSeed,
                    phase1Seed,
                    phase3Seed,
                    phase3ImageKey,
                    requesterName,
                    portraitKey,
                    title,
                    description,
                    effectIds));
            }
            return result;
        }

        private static void ValidateUniqueEffects(
            IReadOnlyList<RawEffect> effects,
            List<OutgameRequestValidationError> errors)
        {
            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < effects.Count; i++)
            {
                RawEffect effect = effects[i];
                if (string.IsNullOrEmpty(effect.EffectId)) continue;
                if (seen.ContainsKey(effect.EffectId))
                {
                    errors.Add(Error(
                        "request_effects.csv", effect.RowNumber, "EffectId",
                        "EffectId must be unique.", effect.EffectId));
                }
                else
                {
                    seen.Add(effect.EffectId, effect.RowNumber);
                }
            }
        }

        private static void ValidateRequests(
            IReadOnlyList<RawRequest> requests,
            IReadOnlyList<RawEffect> effects,
            List<OutgameRequestValidationError> errors)
        {
            var requestIds = new HashSet<string>(StringComparer.Ordinal);
            var seedOwners = new Dictionary<int, string>();
            var seedTupleOwners = new Dictionary<string, string>(StringComparer.Ordinal);
            var effectsById = new Dictionary<string, RawEffect>(StringComparer.Ordinal);
            for (int i = 0; i < effects.Count; i++)
            {
                RawEffect effect = effects[i];
                if (!string.IsNullOrEmpty(effect.EffectId) && !effectsById.ContainsKey(effect.EffectId))
                    effectsById.Add(effect.EffectId, effect);
            }

            int enabledCount = 0;
            for (int i = 0; i < requests.Count; i++)
            {
                RawRequest request = requests[i];
                if (request.Enabled) enabledCount++;
                if (!string.IsNullOrEmpty(request.RequestId) && !requestIds.Add(request.RequestId))
                {
                    errors.Add(Error(
                        "requests.csv", request.RowNumber, "RequestId",
                        "RequestId must be unique.", request.RequestId));
                }
                if (request.PermanentSeed > 0)
                {
                    if (seedOwners.TryGetValue(request.PermanentSeed, out string owner) &&
                        !string.Equals(owner, request.RequestId, StringComparison.Ordinal))
                    {
                        errors.Add(Error(
                            "requests.csv", request.RowNumber, "PermanentSeed",
                            $"PermanentSeed is already owned by RequestId '{owner}'.",
                            request.PermanentSeed.ToString(CultureInfo.InvariantCulture)));
                    }
                    else if (!seedOwners.ContainsKey(request.PermanentSeed))
                    {
                        seedOwners.Add(request.PermanentSeed, request.RequestId);
                    }
                }
                if (request.PermanentSeed > 0 && request.Phase1Seed > 0 && request.Phase3Seed > 0)
                {
                    string tuple = request.PermanentSeed.ToString(CultureInfo.InvariantCulture) + ":" +
                        request.Phase1Seed.ToString(CultureInfo.InvariantCulture) + ":" +
                        request.Phase3Seed.ToString(CultureInfo.InvariantCulture);
                    if (seedTupleOwners.TryGetValue(tuple, out string tupleOwner) &&
                        !string.Equals(tupleOwner, request.RequestId, StringComparison.Ordinal))
                    {
                        errors.Add(Error(
                            "requests.csv", request.RowNumber, "Phase3Seed",
                            $"The complete seed tuple is already owned by RequestId '{tupleOwner}'.", tuple));
                    }
                    else if (!seedTupleOwners.ContainsKey(tuple))
                    {
                        seedTupleOwners.Add(tuple, request.RequestId);
                    }
                }

                for (int effectIndex = 0; effectIndex < request.EffectIds.Count; effectIndex++)
                {
                    string effectId = request.EffectIds[effectIndex];
                    if (!effectsById.TryGetValue(effectId, out RawEffect effect))
                    {
                        errors.Add(Error(
                            "requests.csv", request.RowNumber, $"Effect{effectIndex + 1}Id",
                            "Referenced EffectId does not exist.", effectId));
                    }
                    else if (!effect.Enabled)
                    {
                        errors.Add(Error(
                            "requests.csv", request.RowNumber, $"Effect{effectIndex + 1}Id",
                            "Referenced EffectId is disabled.", effectId));
                    }
                }
            }

            if (enabledCount == 0)
            {
                errors.Add(Error(
                    "requests.csv", 0, "Enabled",
                    "At least one enabled request is required.", "0"));
            }
        }

        private static bool ParseBoolean(
            CsvRow row,
            int index,
            string column,
            string fileKind,
            List<OutgameRequestValidationError> errors)
        {
            string value = row[index].Trim();
            if (string.Equals(value, "TRUE", StringComparison.Ordinal)) return true;
            if (string.Equals(value, "FALSE", StringComparison.Ordinal)) return false;
            errors.Add(Error(
                fileKind, row.RowNumber, column,
                "Enabled must be exactly TRUE or FALSE.", value));
            return false;
        }

        private static RequestType ParseRequestType(
            CsvRow row,
            int index,
            List<OutgameRequestValidationError> errors)
        {
            string value = row[index].Trim();
            if (string.Equals(value, nameof(RequestType.Normal), StringComparison.Ordinal))
                return RequestType.Normal;
            if (string.Equals(value, nameof(RequestType.Sudden), StringComparison.Ordinal))
                return RequestType.Sudden;
            errors.Add(Error(
                "requests.csv", row.RowNumber, "RequestType",
                "RequestType must be existing Normal or Sudden.", value));
            return default;
        }

        private static GameDifficulty ParseDifficulty(
            CsvRow row,
            int index,
            List<OutgameRequestValidationError> errors)
        {
            string value = row[index].Trim();
            if (string.Equals(value, nameof(GameDifficulty.Easy), StringComparison.Ordinal))
                return GameDifficulty.Easy;
            if (string.Equals(value, nameof(GameDifficulty.Normal), StringComparison.Ordinal))
                return GameDifficulty.Normal;
            if (string.Equals(value, nameof(GameDifficulty.Hard), StringComparison.Ordinal))
                return GameDifficulty.Hard;
            errors.Add(Error(
                "requests.csv", row.RowNumber, "Difficulty",
                "Difficulty must be existing Easy, Normal, or Hard.", value));
            return GameDifficulty.Unspecified;
        }

        private static int ParseSeed(
            CsvRow row,
            int index,
            string column,
            List<OutgameRequestValidationError> errors)
        {
            string value = row[index].Trim();
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int seed))
            {
                errors.Add(Error(
                    "requests.csv", row.RowNumber, column,
                    column + " must be a valid int.", value));
                return 0;
            }
            if (seed <= 0)
            {
                errors.Add(Error(
                    "requests.csv", row.RowNumber, column,
                    column + " must be greater than zero.", value));
            }
            return seed;
        }

        private static void Require(
            CsvRow row,
            int index,
            string column,
            string value,
            string fileKind,
            List<OutgameRequestValidationError> errors)
        {
            if (!string.IsNullOrWhiteSpace(value)) return;
            errors.Add(Error(
                fileKind, row.RowNumber, column,
                "Required value is missing or whitespace.", row[index]));
        }

        private static string NormalizeId(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static string ConvertDescription(string value)
        {
            return (value ?? string.Empty).Replace("\\n", "\n");
        }

        private static OutgameRequestValidationError Error(
            string fileKind,
            int row,
            string column,
            string reason,
            string actual)
        {
            return new OutgameRequestValidationError(fileKind, row, column, reason, actual);
        }

        private sealed class CsvDocument
        {
            public static readonly CsvDocument Invalid = new CsvDocument(false, Array.Empty<CsvRow>());

            public CsvDocument(bool headerValid, IEnumerable<CsvRow> rows)
            {
                HeaderValid = headerValid;
                Rows = Array.AsReadOnly(new List<CsvRow>(rows).ToArray());
            }

            public bool HeaderValid { get; }
            public IReadOnlyList<CsvRow> Rows { get; }
        }

        private sealed class CsvRow
        {
            public CsvRow(int rowNumber, IReadOnlyList<string> fields)
            {
                RowNumber = rowNumber;
                Fields = fields;
            }

            public int RowNumber { get; }
            public IReadOnlyList<string> Fields { get; }
            public string this[int index] => Fields[index];
        }

        private sealed class RawEffect
        {
            public RawEffect(
                int rowNumber,
                string effectId,
                bool enabled,
                string effectName,
                string effectIconKey,
                string effectScriptKey,
                string description)
            {
                RowNumber = rowNumber;
                EffectId = effectId;
                Enabled = enabled;
                EffectName = effectName;
                EffectIconKey = effectIconKey;
                EffectScriptKey = effectScriptKey;
                Description = description;
            }

            public int RowNumber { get; }
            public string EffectId { get; }
            public bool Enabled { get; }
            public string EffectName { get; }
            public string EffectIconKey { get; }
            public string EffectScriptKey { get; }
            public string Description { get; }
        }

        private sealed class RawRequest
        {
            public RawRequest(
                int rowNumber,
                string requestId,
                bool enabled,
                RequestType requestType,
                GameDifficulty difficulty,
                int permanentSeed,
                int phase1Seed,
                int phase3Seed,
                string phase3ImageKey,
                string requesterName,
                string portraitKey,
                string title,
                string description,
                IReadOnlyList<string> effectIds)
            {
                RowNumber = rowNumber;
                RequestId = requestId;
                Enabled = enabled;
                RequestType = requestType;
                Difficulty = difficulty;
                PermanentSeed = permanentSeed;
                Phase1Seed = phase1Seed;
                Phase3Seed = phase3Seed;
                Phase3ImageKey = phase3ImageKey;
                RequesterName = requesterName;
                PortraitKey = portraitKey;
                Title = title;
                Description = description;
                EffectIds = effectIds;
            }

            public int RowNumber { get; }
            public string RequestId { get; }
            public bool Enabled { get; }
            public RequestType RequestType { get; }
            public GameDifficulty Difficulty { get; }
            public int PermanentSeed { get; }
            public int Phase1Seed { get; }
            public int Phase3Seed { get; }
            public string Phase3ImageKey { get; }
            public string RequesterName { get; }
            public string PortraitKey { get; }
            public string Title { get; }
            public string Description { get; }
            public IReadOnlyList<string> EffectIds { get; }
        }
    }
}
