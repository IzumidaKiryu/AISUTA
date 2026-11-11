using System;
using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>効果音の種類。</summary>
    public enum SeId
    {
        ItemGet,
        OrbHit,
        Damage,
        Jump,
        HeartBreak,
        Fever,
        Encore,
        SuperFever,
        Versus,
        Fight,
        Finish,
        UiTap,
        UiDecide,
        Win,
        Lose,
    }

    /// <summary>
    /// 効果音の割り当て。
    ///
    /// **音源が入っていないものは、コードで合成した仮の音が鳴る**（<see cref="SeSynth"/>）。
    /// 本番の音源が用意できたら、該当する行に AudioClip を入れるだけで差し替わる。
    /// </summary>
    [CreateAssetMenu(fileName = "SeSettings", menuName = "Yakiniku/Run/SE Settings")]
    public sealed class SeSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SeId id;

            [Tooltip("空なら仮の合成音が鳴る")]
            public AudioClip clip;

            [Range(0f, 1f)] public float volume = 0.7f;
        }

        [Range(0f, 1f)] public float masterVolume = 0.8f;

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry Get(SeId id)
        {
            foreach (var entry in entries)
            {
                if (entry != null && entry.id == id) return entry;
            }

            return null;
        }

        public const string ResourcePath = "SeSettings";
    }
}
