using System;
using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.GameFlow
{
    public enum GameSfxId
    {
        Hit,
        PowerHit,
        TileCrack,
        TileDestroy,
        Brush,
        Basket,
        Snap,
        Cutter,
        Grinder,
        Clear,
        Fail,
        Logout,
        Click,
        Use,
        Time
    }

    [DisallowMultipleComponent]
    public sealed class GameSfxPlayer : MonoBehaviour
    {
        private readonly struct ClipDefinition
        {
            public ClipDefinition(GameSfxId id, string resourceName, float gain)
            {
                Id = id;
                ResourceName = resourceName;
                Gain = gain;
            }

            public GameSfxId Id { get; }
            public string ResourceName { get; }
            public float Gain { get; }
        }

        private static readonly ClipDefinition[] Definitions =
        {
            new ClipDefinition(GameSfxId.Hit, "SE_hit", 0.46f),
            new ClipDefinition(GameSfxId.PowerHit, "SE_Powerhit", 0.55f),
            new ClipDefinition(GameSfxId.TileCrack, "SE_tilecrack", 0.42f),
            new ClipDefinition(GameSfxId.TileDestroy, "SE_tileDestroy", 0.48f),
            new ClipDefinition(GameSfxId.Brush, "SE_brush", 0.30f),
            new ClipDefinition(GameSfxId.Basket, "SE_basket", 0.55f),
            new ClipDefinition(GameSfxId.Snap, "SE_snap", 0.72f),
            new ClipDefinition(GameSfxId.Cutter, "SE_cutter", 0.85f),
            new ClipDefinition(GameSfxId.Grinder, "SE_grinder", 0.55f),
            new ClipDefinition(GameSfxId.Clear, "SE_Clear", 0.58f),
            new ClipDefinition(GameSfxId.Fail, "SE_Fail", 0.50f),
            new ClipDefinition(GameSfxId.Logout, "SE_logout", 0.65f),
            new ClipDefinition(GameSfxId.Click, "SE_Click", 0.45f),
            new ClipDefinition(GameSfxId.Use, "SE_USE", 0.70f),
            new ClipDefinition(GameSfxId.Time, "SE_Time", 1.10f)
        };

        private const string ResourceRoot = "Sound/SE/";
        private const int PoolSize = 8;
        private const float MasterSfxVolume = 0.72f;
        private const float DuplicateWindowSeconds = 0.025f;

        private readonly Dictionary<GameSfxId, AudioClip> clips = new Dictionary<GameSfxId, AudioClip>();
        private readonly Dictionary<GameSfxId, float> gains = new Dictionary<GameSfxId, float>();
        private readonly Dictionary<GameSfxId, float> lastPlayedAt = new Dictionary<GameSfxId, float>();
        private readonly AudioSource[] pool = new AudioSource[PoolSize];
        private AudioSource timeSource;
        private AudioSource brushSource;
        private int nextSource;
        private bool initialized;

        public static GameSfxPlayer Instance { get; private set; }
        public static bool SfxEnabled => IngameOptionPreferences.SfxEnabled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static GameSfxPlayer EnsureInstance()
        {
            if (Instance) return Instance;
            var root = new GameObject("Game SFX Player");
            DontDestroyOnLoad(root);
            Instance = root.AddComponent<GameSfxPlayer>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
            IngameOptionPreferences.Changed += HandlePreferenceChanged;
        }

        private void OnDestroy()
        {
            IngameOptionPreferences.Changed -= HandlePreferenceChanged;
            if (Instance == this) Instance = null;
        }

        public static bool Play(GameSfxId id)
        {
            return EnsureInstance().PlayInternal(id);
        }

        public static bool StartBrushLoop()
        {
            return EnsureInstance().StartBrushLoopInternal();
        }

        public static void StopBrushLoop()
        {
            if (Instance) Instance.StopBrushLoopInternal();
        }

        public static bool TryPlayLogout(out float duration)
        {
            GameSfxPlayer player = EnsureInstance();
            duration = 0f;
            if (!SfxEnabled || !player.clips.TryGetValue(GameSfxId.Logout, out AudioClip clip) || !clip) return false;
            if (!player.PlayInternal(GameSfxId.Logout)) return false;
            duration = clip.length;
            return duration > 0f;
        }

        public static void StopAllSfx()
        {
            if (Instance) Instance.StopAllInternal();
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            for (int i = 0; i < Definitions.Length; i++)
            {
                ClipDefinition definition = Definitions[i];
                AudioClip clip = Resources.Load<AudioClip>(ResourceRoot + definition.ResourceName);
                if (!clip)
                    Debug.LogError($"[SFX] Missing AudioClip: {ResourceRoot}{definition.ResourceName}", this);
                else if (clip.loadState == AudioDataLoadState.Unloaded)
                    clip.LoadAudioData();
                clips[definition.Id] = clip;
                gains[definition.Id] = definition.Gain;
            }

            for (int i = 0; i < pool.Length; i++) pool[i] = CreateSource();
            timeSource = CreateSource();
            brushSource = CreateSource();
            if (!SfxEnabled) StopAllInternal();
        }

        private AudioSource CreateSource()
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            return source;
        }

        private bool PlayInternal(GameSfxId id)
        {
            if (!SfxEnabled || !clips.TryGetValue(id, out AudioClip clip) || !clip) return false;
            if (id == GameSfxId.Time)
            {
                if (timeSource.isPlaying) return false;
                ConfigureAndPlay(timeSource, clip, gains[id]);
                return true;
            }
            if (id == GameSfxId.Brush)
                return StartBrushLoopInternal();

            float now = Time.unscaledTime;
            if (lastPlayedAt.TryGetValue(id, out float previous) && now - previous < DuplicateWindowSeconds) return false;
            lastPlayedAt[id] = now;

            AudioSource source = FindAvailableSource();
            ConfigureAndPlay(source, clip, gains[id]);
            return true;
        }

        private bool StartBrushLoopInternal()
        {
            if (!SfxEnabled || !clips.TryGetValue(GameSfxId.Brush, out AudioClip clip) || !clip) return false;
            if (brushSource.isPlaying) return true;
            brushSource.loop = true;
            ConfigureAndPlay(brushSource, clip, gains[GameSfxId.Brush]);
            return true;
        }

        private void StopBrushLoopInternal()
        {
            if (!brushSource) return;
            brushSource.Stop();
            brushSource.loop = false;
            brushSource.clip = null;
        }

        private AudioSource FindAvailableSource()
        {
            for (int offset = 0; offset < pool.Length; offset++)
            {
                int index = (nextSource + offset) % pool.Length;
                if (pool[index].isPlaying) continue;
                nextSource = (index + 1) % pool.Length;
                return pool[index];
            }

            AudioSource reused = pool[nextSource];
            reused.Stop();
            nextSource = (nextSource + 1) % pool.Length;
            return reused;
        }

        private static void ConfigureAndPlay(AudioSource source, AudioClip clip, float gain)
        {
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(MasterSfxVolume * gain);
            source.pitch = 1f;
            source.Play();
        }

        private void HandlePreferenceChanged()
        {
            if (!SfxEnabled) StopAllInternal();
        }

        private void StopAllInternal()
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (!pool[i]) continue;
                pool[i].Stop();
                pool[i].clip = null;
            }
            if (timeSource)
            {
                timeSource.Stop();
                timeSource.clip = null;
            }
            if (brushSource)
                StopBrushLoopInternal();
            lastPlayedAt.Clear();
        }
    }
}
