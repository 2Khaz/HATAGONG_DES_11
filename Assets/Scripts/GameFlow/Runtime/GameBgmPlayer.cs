using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HATAGONG.GameFlow
{
    public enum GameBgmId
    {
        Lobby,
        Ingame01,
        Ingame02
    }

    public sealed class GameBgmPlayer : MonoBehaviour
    {
        private readonly struct ClipDefinition
        {
            public ClipDefinition(GameBgmId id, string resourceName, float gain)
            {
                Id = id;
                ResourceName = resourceName;
                Gain = gain;
            }

            public GameBgmId Id { get; }
            public string ResourceName { get; }
            public float Gain { get; }
        }

        private const string ResourceRoot = "Sound/BGM/";
        private const string LobbySceneName = "OUTGAME_LOBBY";
        private const string IngameSceneName = "INGAME";
        private const float MasterGain = 0.55f;
        private const float FadeDuration = 0.35f;

        private static readonly ClipDefinition[] Definitions =
        {
            new ClipDefinition(GameBgmId.Lobby, "BGM_Lobby", 0.95f),
            new ClipDefinition(GameBgmId.Ingame01, "BGM_Ingame01", 1.00f),
            new ClipDefinition(GameBgmId.Ingame02, "BGM_Ingame02", 0.98f)
        };

        private readonly Dictionary<GameBgmId, AudioClip> clips = new Dictionary<GameBgmId, AudioClip>();
        private readonly Dictionary<GameBgmId, float> gains = new Dictionary<GameBgmId, float>();
        private readonly GameBgmId[] ingamePool = { GameBgmId.Ingame01, GameBgmId.Ingame02 };

        private AudioSource source;
        private Coroutine transition;
        private AudioClip requestedClip;
        private GameBgmId? currentId;
        private GameBgmId? selectedIngameId;
        private System.Random random;

        public static GameBgmPlayer Instance { get; private set; }
        public GameBgmId? CurrentId => currentId;
        public GameBgmId? SelectedIngameId => selectedIngameId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static GameBgmPlayer EnsureInstance()
        {
            if (Instance) return Instance;
            var root = new GameObject(nameof(GameBgmPlayer));
            DontDestroyOnLoad(root);
            Instance = root.AddComponent<GameBgmPlayer>();
            return Instance;
        }

        public static void BeginNewIngameRequest()
        {
            EnsureInstance().SelectAndPlayNewIngameTrack();
        }

        public static void RestoreLobbyAfterRequestLoadFailure()
        {
            EnsureInstance().PlayLobby();
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
            random = new System.Random(unchecked(Environment.TickCount ^ GetInstanceID()));
            LoadClipsOnce();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.mute = !IngameOptionPreferences.BgmEnabled;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            IngameOptionPreferences.Changed += HandlePreferenceChanged;
        }

        private void Start()
        {
            ApplySceneState(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            IngameOptionPreferences.Changed -= HandlePreferenceChanged;
            Instance = null;
        }

        private void LoadClipsOnce()
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                ClipDefinition definition = Definitions[i];
                AudioClip clip = Resources.Load<AudioClip>(ResourceRoot + definition.ResourceName);
                if (!clip)
                {
                    Debug.LogError($"[BGM] Missing AudioClip: {ResourceRoot}{definition.ResourceName}", this);
                    continue;
                }

                clips[definition.Id] = clip;
                gains[definition.Id] = definition.Gain;
                clip.LoadAudioData();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneState(scene);
        }

        private void ApplySceneState(Scene scene)
        {
            if (string.Equals(scene.name, LobbySceneName, StringComparison.Ordinal))
            {
                PlayLobby();
                return;
            }

            if (!string.Equals(scene.name, IngameSceneName, StringComparison.Ordinal)) return;
            if (!selectedIngameId.HasValue) SelectAndPlayNewIngameTrack();
            else RequestTrack(selectedIngameId.Value);
        }

        private void PlayLobby()
        {
            RequestTrack(GameBgmId.Lobby);
        }

        private void SelectAndPlayNewIngameTrack()
        {
            int index = random.Next(ingamePool.Length);
            selectedIngameId = ingamePool[index];
            RequestTrack(selectedIngameId.Value);
        }

        private void RequestTrack(GameBgmId id)
        {
            if (!clips.TryGetValue(id, out AudioClip clip) || !clip) return;
            float targetVolume = MasterGain * gains[id];

            if (requestedClip == clip && transition != null) return;
            requestedClip = clip;

            if (transition != null)
            {
                StopCoroutine(transition);
                transition = null;
            }

            if (source.clip == clip && source.isPlaying)
            {
                currentId = id;
                transition = StartCoroutine(FadeVolume(source.volume, targetVolume));
                return;
            }

            transition = StartCoroutine(SwitchTrack(id, clip, targetVolume));
        }

        private IEnumerator SwitchTrack(GameBgmId id, AudioClip clip, float targetVolume)
        {
            if (source.isPlaying && source.clip)
                yield return FadeVolume(source.volume, 0f);

            source.Stop();
            source.clip = clip;
            source.volume = 0f;
            source.Play();
            currentId = id;
            yield return FadeVolume(0f, targetVolume);
            transition = null;
        }

        private IEnumerator FadeVolume(float from, float to)
        {
            if (Mathf.Approximately(from, to))
            {
                source.volume = to;
                transition = null;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < FadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / FadeDuration));
                yield return null;
            }

            source.volume = to;
            transition = null;
        }

        private void HandlePreferenceChanged()
        {
            source.mute = !IngameOptionPreferences.BgmEnabled;
        }
    }
}
