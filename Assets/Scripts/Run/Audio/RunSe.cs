using System.Collections.Generic;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 効果音を鳴らす窓口。どのシーンからでも <c>RunSe.Play(SeId.ItemGet)</c> で鳴る。
    ///
    /// 鳴らすための AudioSource はシーンをまたいで1つだけ持つ
    /// （リザルトへ遷移した瞬間に勝利音が途切れないように）。
    /// </summary>
    public static class RunSe
    {
        private static AudioSource source;
        private static SeSettings settings;
        private static readonly Dictionary<SeId, AudioClip> synthesized = new Dictionary<SeId, AudioClip>();

        // 同じ音が同じフレームに何十回も重なると割れるので、最低間隔を空ける
        private static readonly Dictionary<SeId, float> lastPlayed = new Dictionary<SeId, float>();
        private const float MinInterval = 0.03f;

        public static void Play(SeId id)
        {
            EnsureSource();

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < MinInterval) return;
            lastPlayed[id] = now;

            var entry = settings != null ? settings.Get(id) : null;
            var clip = entry != null && entry.clip != null ? entry.clip : Synthesized(id);
            if (clip == null) return;

            float volume = (entry != null ? entry.volume : 0.7f) * (settings != null ? settings.masterVolume : 0.8f);
            source.PlayOneShot(clip, volume);
        }

        private static AudioClip Synthesized(SeId id)
        {
            if (!synthesized.TryGetValue(id, out var clip) || clip == null)
            {
                clip = SeSynth.Create(id);
                synthesized[id] = clip;
            }

            return clip;
        }

        private static void EnsureSource()
        {
            if (source != null) return;

            var go = new GameObject("~RunSe");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D

            settings = Resources.Load<SeSettings>(SeSettings.ResourcePath);
        }
    }
}
