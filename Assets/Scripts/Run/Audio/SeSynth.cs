using UnityEngine;

namespace Yakiniku.Run
{
    /// <summary>
    /// 仮の効果音をコードで合成する。本番の音源が無い間のつなぎ。
    ///
    /// 音の性格だけ本家に寄せてある（取得は高くて短い、被弾は低くて濁る、
    /// フィーバー系は上がっていく和音）。音源が用意できたら <see cref="SeSettings"/> に
    /// 入れればこちらは使われなくなる。
    /// </summary>
    public static class SeSynth
    {
        private const int SampleRate = 44100;

        public static AudioClip Create(SeId id)
        {
            switch (id)
            {
                case SeId.ItemGet:    return Tone("ItemGet", 0.09f, 1568f, 2349f, Wave.Sine, 30f);
                case SeId.OrbHit:     return Tone("OrbHit", 0.07f, 988f, 988f, Wave.Triangle, 45f);
                case SeId.Damage:     return Tone("Damage", 0.25f, 220f, 110f, Wave.Square, 10f);
                case SeId.Jump:       return Tone("Jump", 0.14f, 523f, 1046f, Wave.Triangle, 12f);
                case SeId.HeartBreak: return Arpeggio("HeartBreak", new[] { 1318f, 988f, 784f }, 0.07f, Wave.Sine);
                case SeId.Fever:      return Arpeggio("Fever", new[] { 523f, 659f, 784f, 1046f }, 0.07f, Wave.Triangle);
                case SeId.Encore:     return Arpeggio("Encore", new[] { 523f, 659f, 784f, 1046f, 1318f, 1568f }, 0.06f, Wave.Triangle);
                case SeId.SuperFever: return Arpeggio("SuperFever", new[] { 784f, 988f, 1175f, 1568f, 1976f, 2349f }, 0.045f, Wave.Sine);
                case SeId.Versus:     return Tone("Versus", 0.35f, 196f, 392f, Wave.Square, 6f);
                case SeId.Fight:      return Arpeggio("Fight", new[] { 392f, 523f, 784f }, 0.09f, Wave.Square);
                case SeId.Finish:     return Chord("Finish", new[] { 523f, 659f, 784f, 1046f }, 0.9f);
                case SeId.UiTap:      return Tone("UiTap", 0.04f, 1760f, 1760f, Wave.Sine, 60f);
                case SeId.UiDecide:   return Arpeggio("UiDecide", new[] { 1318f, 1760f }, 0.05f, Wave.Sine);
                case SeId.Win:        return Arpeggio("Win", new[] { 523f, 659f, 784f, 1046f, 784f, 1046f }, 0.1f, Wave.Triangle);
                case SeId.Lose:       return Arpeggio("Lose", new[] { 523f, 466f, 392f, 330f }, 0.14f, Wave.Triangle);
                default:              return Tone("Default", 0.05f, 880f, 880f, Wave.Sine, 40f);
            }
        }

        private enum Wave { Sine, Triangle, Square }

        /// <summary>周波数を start→end へ滑らせる単音。decay が大きいほど早く消える。</summary>
        private static AudioClip Tone(string name, float seconds, float startHz, float endHz, Wave wave, float decay)
        {
            int length = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[length];
            double phase = 0;

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)length;
                float hz = Mathf.Lerp(startHz, endHz, progress);

                phase += hz / SampleRate;
                float envelope = Attack(i) * Mathf.Exp(-decay * t);
                data[i] = Sample(wave, phase) * envelope * 0.6f;
            }

            return Build(name, data);
        }

        /// <summary>音を順番に鳴らす（上がっていくキラキラ音）。</summary>
        private static AudioClip Arpeggio(string name, float[] notes, float step, Wave wave)
        {
            float tail = 0.25f;
            int length = Mathf.CeilToInt((notes.Length * step + tail) * SampleRate);
            var data = new float[length];

            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.FloorToInt(n * step * SampleRate);
                double phase = 0;

                for (int i = start; i < length; i++)
                {
                    float t = (i - start) / (float)SampleRate;
                    phase += notes[n] / SampleRate;
                    float envelope = Attack(i - start) * Mathf.Exp(-9f * t);
                    data[i] += Sample(wave, phase) * envelope * 0.35f;
                }
            }

            Normalize(data);
            return Build(name, data);
        }

        /// <summary>和音を同時に鳴らす。</summary>
        private static AudioClip Chord(string name, float[] notes, float seconds)
        {
            int length = Mathf.CeilToInt(seconds * SampleRate);
            var data = new float[length];

            foreach (float hz in notes)
            {
                double phase = 0;
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)SampleRate;
                    phase += hz / SampleRate;
                    data[i] += Sample(Wave.Triangle, phase) * Attack(i) * Mathf.Exp(-3f * t) * 0.3f;
                }
            }

            Normalize(data);
            return Build(name, data);
        }

        private static float Sample(Wave wave, double phase)
        {
            float p = (float)(phase - System.Math.Floor(phase));
            switch (wave)
            {
                case Wave.Square:   return p < 0.5f ? 0.5f : -0.5f; // 矩形は耳に痛いので小さめ
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
                default:            return Mathf.Sin(p * Mathf.PI * 2f);
            }
        }

        /// <summary>立ち上がりを数ミリ秒なめらかにして、プツッというノイズを防ぐ。</summary>
        private static float Attack(int sample) => Mathf.Clamp01(sample / (SampleRate * 0.004f));

        private static void Normalize(float[] data)
        {
            float peak = 0f;
            foreach (float v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak <= 0.8f) return;

            float scale = 0.8f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= scale;
        }

        private static AudioClip Build(string name, float[] data)
        {
            var clip = AudioClip.Create("SE_" + name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
