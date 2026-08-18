using UnityEngine;

namespace Framework.Audio
{
    /// <summary>音量设置；支持 PlayerPrefs 持久化。</summary>
    public sealed class AudioVolumeSettings
    {
        const string KeyMaster = "Framework.Audio.MasterVolume";
        const string KeyBgm = "Framework.Audio.BgmVolume";
        const string KeySfx = "Framework.Audio.SfxVolume";
        const string KeyMute = "Framework.Audio.Mute";

        /// <summary>主音量，范围 [0, 1]。</summary>
        public float MasterVolume { get; set; } = 1f;

        /// <summary>BGM 音量，范围 [0, 1]。</summary>
        public float BgmVolume { get; set; } = 1f;

        /// <summary>SFX 音量，范围 [0, 1]。</summary>
        public float SfxVolume { get; set; } = 1f;

        /// <summary>全局静音。</summary>
        public bool Mute { get; set; }

        /// <summary>从 PlayerPrefs 加载音量设置。</summary>
        public void Load()
        {
            MasterVolume = PlayerPrefs.GetFloat(KeyMaster, 1f);
            BgmVolume = PlayerPrefs.GetFloat(KeyBgm, 1f);
            SfxVolume = PlayerPrefs.GetFloat(KeySfx, 1f);
            Mute = PlayerPrefs.GetInt(KeyMute, 0) != 0;
        }

        /// <summary>保存音量设置到 PlayerPrefs。</summary>
        public void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, Mathf.Clamp01(MasterVolume));
            PlayerPrefs.SetFloat(KeyBgm, Mathf.Clamp01(BgmVolume));
            PlayerPrefs.SetFloat(KeySfx, Mathf.Clamp01(SfxVolume));
            PlayerPrefs.SetInt(KeyMute, Mute ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>计算 BGM 通道有效音量。</summary>
        /// <param name="scale">单次播放音量缩放。</param>
        /// <returns>有效音量。</returns>
        public float GetBgmVolume(float scale = 1f)
        {
            return Mute ? 0f : Mathf.Clamp01(MasterVolume * BgmVolume * scale);
        }

        /// <summary>计算 SFX 通道有效音量。</summary>
        /// <param name="scale">单次播放音量缩放。</param>
        /// <returns>有效音量。</returns>
        public float GetSfxVolume(float scale = 1f)
        {
            return Mute ? 0f : Mathf.Clamp01(MasterVolume * SfxVolume * scale);
        }
    }
}
