using System.Collections.Generic;
using UnityEngine;

/// <summary>Все звуки игры.</summary>
public enum Sfx { Coin, Jump, Hurt, Checkpoint, Win, GameOver, Stomp, Heart, Dash, Spring, PowerUp, Crumble }

/// <summary>
/// Звуковые эффекты в стиле старых приставок. Их не надо скачивать:
/// звук "рисуется" прямо кодом — это просто массив чисел от -1 до 1
/// (форма звуковой волны), из которого Unity делает AudioClip.
///
/// Использование из любого скрипта: SoundFX.Play(Sfx.Coin);
///
/// Хочешь свои звуки? Положи .wav в проект, сделай поле AudioClip
/// и вызывай audioSource.PlayOneShot(clip) — это стандартный способ.
/// </summary>
public static class SoundFX
{
    /// <summary>Общая громкость эффектов (0..1).</summary>
    public static float Volume = 0.35f;

    private const int SampleRate = 44100;
    private static AudioSource source;
    private static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();

    private enum Wave { Square, Triangle, Noise }

    public static void Play(Sfx sound)
    {
        if (source == null)
        {
            // Один невидимый объект с AudioSource на всю игру.
            // DontDestroyOnLoad — объект не удаляется при смене сцены.
            var go = new GameObject("SoundFX");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        if (!clips.TryGetValue(sound, out AudioClip clip) || clip == null)
        {
            clip = Create(sound);
            clips[sound] = clip;
        }
        source.PlayOneShot(clip, Volume);
    }

    // Частоты нот (Гц): до-5 = 523, ми-5 = 659, соль-5 = 784, до-6 = 1047.
    private static AudioClip Create(Sfx sound)
    {
        switch (sound)
        {
            case Sfx.Coin:       return Notes("Coin", Wave.Square, 0.07f, 988f, 1319f);
            case Sfx.Jump:       return Sweep("Jump", Wave.Square, 0.14f, 260f, 620f);
            case Sfx.Hurt:       return Sweep("Hurt", Wave.Noise, 0.3f, 900f, 120f);
            case Sfx.Stomp:      return Sweep("Stomp", Wave.Triangle, 0.15f, 420f, 80f);
            case Sfx.Checkpoint: return Notes("Checkpoint", Wave.Triangle, 0.09f, 523f, 659f, 784f);
            case Sfx.Heart:      return Notes("Heart", Wave.Triangle, 0.08f, 659f, 880f, 1175f);
            case Sfx.Win:        return Notes("Win", Wave.Square, 0.12f, 523f, 659f, 784f, 1047f, 784f, 1047f);
            case Sfx.GameOver:   return Notes("GameOver", Wave.Triangle, 0.22f, 392f, 330f, 262f, 196f);
            case Sfx.Dash:       return Sweep("Dash", Wave.Noise, 0.12f, 1400f, 300f);
            case Sfx.Spring:     return Sweep("Spring", Wave.Square, 0.25f, 180f, 900f);
            case Sfx.PowerUp:    return Notes("PowerUp", Wave.Square, 0.06f, 523f, 659f, 784f, 1047f, 1319f, 1568f);
            case Sfx.Crumble:    return Sweep("Crumble", Wave.Noise, 0.3f, 500f, 60f);
            default:             return Notes("Beep", Wave.Square, 0.05f, 660f);
        }
    }

    /// <summary>Несколько нот подряд, каждая длиной noteLength секунд.</summary>
    private static AudioClip Notes(string name, Wave wave, float noteLength, params float[] frequencies)
    {
        int perNote = (int)(SampleRate * noteLength);
        var data = new float[perNote * frequencies.Length];
        for (int n = 0; n < frequencies.Length; n++)
        {
            float phase = 0f;
            for (int i = 0; i < perNote; i++)
            {
                phase += frequencies[n] / SampleRate;
                data[n * perNote + i] = Sample(wave, phase) * Envelope(i, perNote);
            }
        }
        return MakeClip(name, data);
    }

    /// <summary>Звук, частота которого плавно едет от fromHz к toHz ("вжух").</summary>
    private static AudioClip Sweep(string name, Wave wave, float length, float fromHz, float toHz)
    {
        int count = (int)(SampleRate * length);
        var data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = (float)i / count;
            phase += Mathf.Lerp(fromHz, toHz, t) / SampleRate;
            data[i] = Sample(wave, phase) * Envelope(i, count);
        }
        return MakeClip(name, data);
    }

    private static float Sample(Wave wave, float phase)
    {
        float p = phase - Mathf.Floor(phase); // 0..1 — где мы внутри одного колебания
        switch (wave)
        {
            case Wave.Square:   return p < 0.5f ? 0.5f : -0.5f;
            case Wave.Triangle: return 1f - 4f * Mathf.Abs(p - 0.5f);
            default:            return Random.Range(-0.6f, 0.6f) * (p < 0.5f ? 1f : 0.6f);
        }
    }

    // Громкость внутри ноты: быстро нарастает и плавно затухает — без щелчков.
    private static float Envelope(int i, int length)
    {
        float attack = Mathf.Clamp01(i / (SampleRate * 0.005f));
        float release = 1f - (float)i / length;
        return attack * release;
    }

    private static AudioClip MakeClip(string name, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
