using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace HATAGONG.Outgame
{
    public static class OutgameRequestTableLoader
    {
        public const string RequestsFileName = "requests.csv";
        public const string EffectsFileName = "request_effects.csv";
        private const string DataDirectory = "Data";

        public static async Task<OutgameRequestTableLoadResult> LoadAsync()
        {
            string requestsUri = BuildStreamingAssetsUri(RequestsFileName);
            string effectsUri = BuildStreamingAssetsUri(EffectsFileName);
            TextLoadResult requests = await LoadTextAsync(requestsUri, RequestsFileName);
            TextLoadResult effects = await LoadTextAsync(effectsUri, EffectsFileName);

            if (!requests.Success || !effects.Success)
            {
                var errors = new List<OutgameRequestValidationError>();
                if (!requests.Success) errors.Add(requests.Error);
                if (!effects.Success) errors.Add(effects.Error);
                return OutgameRequestTableLoadResult.Failed(errors);
            }

            return OutgameRequestCatalog.LoadFromCsv(requests.Text, effects.Text);
        }

        public static string BuildStreamingAssetsUri(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("StreamingAssets file name is required.", nameof(fileName));
            if (fileName.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new ArgumentException("Only a file name may be supplied.", nameof(fileName));

            string root = Application.streamingAssetsPath;
            if (string.IsNullOrWhiteSpace(root))
                throw new InvalidOperationException("Application.streamingAssetsPath is empty.");

            string escapedRelative = Uri.EscapeDataString(DataDirectory) + "/" + Uri.EscapeDataString(fileName);
            if (root.IndexOf("://", StringComparison.Ordinal) >= 0)
                return root.TrimEnd('/', '\\') + "/" + escapedRelative;

            string fullPath = Path.GetFullPath(Path.Combine(root, DataDirectory, fileName));
            return new Uri(fullPath).AbsoluteUri;
        }

        private static async Task<TextLoadResult> LoadTextAsync(string uri, string fileKind)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(uri))
            {
                request.timeout = 15;
                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                if (!operation.isDone)
                {
                    var completion = new TaskCompletionSource<bool>();
                    operation.completed += _ => completion.TrySetResult(true);
                    await completion.Task;
                }

                if (request.result != UnityWebRequest.Result.Success)
                {
                    return TextLoadResult.Failed(new OutgameRequestValidationError(
                        fileKind,
                        0,
                        string.Empty,
                        $"UnityWebRequest failed: {request.error}",
                        uri));
                }
                return TextLoadResult.Succeeded(request.downloadHandler.text);
            }
        }

        private sealed class TextLoadResult
        {
            private TextLoadResult(bool success, string text, OutgameRequestValidationError error)
            {
                Success = success;
                Text = text;
                Error = error;
            }

            public bool Success { get; }
            public string Text { get; }
            public OutgameRequestValidationError Error { get; }

            public static TextLoadResult Succeeded(string text)
            {
                return new TextLoadResult(true, text ?? string.Empty, null);
            }

            public static TextLoadResult Failed(OutgameRequestValidationError error)
            {
                return new TextLoadResult(false, string.Empty, error);
            }
        }
    }
}
