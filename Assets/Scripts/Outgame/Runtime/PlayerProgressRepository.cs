using System;
using System.IO;
using System.Text;
using UnityEngine;
using HATAGONG.GameFlow;

namespace HATAGONG.Outgame
{
    public static class PlayerProgressRepository
    {
        private const string FileName = "hatagong-player-progress-v1.json";
        private static readonly object Sync = new object();

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static PlayerProgressData Load()
        {
            lock (Sync)
            {
                return LoadUnlocked();
            }
        }

        public static int GetClearedStageCount()
        {
            return Load().ClearedStageCount;
        }

        public static bool Save(PlayerProgressData data)
        {
            lock (Sync)
            {
                return SaveUnlocked(Normalize(data));
            }
        }

        public static bool RecordStageClear(out int clearedStageCount)
        {
            lock (Sync)
            {
                PlayerProgressData data = LoadUnlocked();
                if (data.ClearedStageCount < int.MaxValue) data.ClearedStageCount++;
                clearedStageCount = data.ClearedStageCount;
                return SaveUnlocked(data);
            }
        }

        public static int GetItemQuantity(GameItemId itemId)
        {
            lock (Sync) return GetItemQuantity(LoadUnlocked(), itemId);
        }

        public static bool TryConsumeItem(GameItemId itemId)
        {
            lock (Sync)
            {
                PlayerProgressData data = LoadUnlocked();
                int quantity = GetItemQuantity(data, itemId);
                if (quantity <= 0) return false;
                SetItemQuantity(data, itemId, quantity - 1);
                return SaveUnlocked(data);
            }
        }

        public static bool GrantItem(GameItemId itemId, int amount)
        {
            if (amount <= 0) return false;
            lock (Sync)
            {
                PlayerProgressData data = LoadUnlocked();
                int current = GetItemQuantity(data, itemId);
                SetItemQuantity(data, itemId, Math.Min(99, current + amount));
                return SaveUnlocked(data);
            }
        }

        public static bool GrantFullClearItemReward()
        {
            lock (Sync)
            {
                PlayerProgressData data = LoadUnlocked();
                AddItem(data, GameItemId.Hammer, 2);
                AddItem(data, GameItemId.Trowel, 2);
                AddItem(data, GameItemId.TileGrinder, 2);
                AddItem(data, GameItemId.Scraper, 1);
                AddItem(data, GameItemId.Stopwatch, 1);
                AddItem(data, GameItemId.CementBasket, 1);
                AddItem(data, GameItemId.TileCutter, 1);
                return SaveUnlocked(data);
            }
        }

        private static PlayerProgressData LoadUnlocked()
        {
            string path = SavePath;
            if (!File.Exists(path)) return new PlayerProgressData(0);

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                PlayerProgressData data = JsonUtility.FromJson<PlayerProgressData>(json);
                if (data == null || data.Version < 1 || data.Version > PlayerProgressData.CurrentVersion)
                {
                    Debug.LogWarning($"[Outgame][Progress] Unsupported or empty progress data. Defaults are used. path={path}");
                    return new PlayerProgressData(0);
                }
                return Normalize(data);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Outgame][Progress] Progress data could not be loaded. Defaults are used. path={path}, error={exception.Message}");
                return new PlayerProgressData(0);
            }
        }

        private static bool SaveUnlocked(PlayerProgressData data)
        {
            string path = SavePath;
            string temporaryPath = path + ".tmp";
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

                string json = JsonUtility.ToJson(Normalize(data), true);
                File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(temporaryPath, path, null);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceWithCopy(temporaryPath, path);
                    }
                    catch (IOException)
                    {
                        ReplaceWithCopy(temporaryPath, path);
                    }
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Outgame][Progress] Progress data could not be saved. path={path}, error={exception}");
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                }
                catch (Exception cleanupException)
                {
                    Debug.LogWarning($"[Outgame][Progress] Temporary progress file cleanup failed. path={temporaryPath}, error={cleanupException.Message}");
                }
            }
        }

        private static void ReplaceWithCopy(string temporaryPath, string path)
        {
            File.Copy(temporaryPath, path, true);
            File.Delete(temporaryPath);
        }

        private static PlayerProgressData Normalize(PlayerProgressData data)
        {
            if (data == null) return new PlayerProgressData(0);
            data.Version = PlayerProgressData.CurrentVersion;
            if (data.ClearedStageCount < 0) data.ClearedStageCount = 0;
            if (!data.ItemInventoryInitialized)
            {
                data.ItemInventoryInitialized = true;
                data.Stopwatch = data.Hammer = data.TileGrinder = data.TileCutter = data.CementBasket = data.Trowel = data.Scraper = 5;
            }
            data.Stopwatch = ClampItem(data.Stopwatch);
            data.Hammer = ClampItem(data.Hammer);
            data.TileGrinder = ClampItem(data.TileGrinder);
            data.TileCutter = ClampItem(data.TileCutter);
            data.CementBasket = ClampItem(data.CementBasket);
            data.Trowel = ClampItem(data.Trowel);
            data.Scraper = ClampItem(data.Scraper);
            return data;
        }

        private static int ClampItem(int value) => Math.Max(0, Math.Min(99, value));
        private static void AddItem(PlayerProgressData data, GameItemId itemId, int amount) =>
            SetItemQuantity(data, itemId, Math.Min(99, GetItemQuantity(data, itemId) + amount));

        private static int GetItemQuantity(PlayerProgressData data, GameItemId itemId)
        {
            switch (itemId)
            {
                case GameItemId.Stopwatch: return data.Stopwatch;
                case GameItemId.Hammer: return data.Hammer;
                case GameItemId.TileGrinder: return data.TileGrinder;
                case GameItemId.TileCutter: return data.TileCutter;
                case GameItemId.CementBasket: return data.CementBasket;
                case GameItemId.Trowel: return data.Trowel;
                case GameItemId.Scraper: return data.Scraper;
                default: return 0;
            }
        }

        private static void SetItemQuantity(PlayerProgressData data, GameItemId itemId, int value)
        {
            value = ClampItem(value);
            switch (itemId)
            {
                case GameItemId.Stopwatch: data.Stopwatch = value; break;
                case GameItemId.Hammer: data.Hammer = value; break;
                case GameItemId.TileGrinder: data.TileGrinder = value; break;
                case GameItemId.TileCutter: data.TileCutter = value; break;
                case GameItemId.CementBasket: data.CementBasket = value; break;
                case GameItemId.Trowel: data.Trowel = value; break;
                case GameItemId.Scraper: data.Scraper = value; break;
            }
        }
    }
}
