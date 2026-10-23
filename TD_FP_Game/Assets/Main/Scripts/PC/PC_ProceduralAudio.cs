using UnityEngine;

/// <summary>
/// Synthesizes the PC's UI sounds at runtime so the terminal sounds like a computer
/// even when no clips are assigned. Clips are generated once and cached.
/// Any clip assigned on PC_DesktopShell takes priority over these.
/// </summary>
public static class PC_ProceduralAudio
{
    private const int SampleRate = 44100;

    private static AudioClip _hover, _click, _success, _error, _boot, _shutdown, _notify, _hum;

    public static AudioClip Hover => _hover ??= Tone("PC_Hover", 0.018f, 2400f, 2400f, 0.25f, Wave.Sine, 120f);
    public static AudioClip Click => _click ??= Tone("PC_Click", 0.035f, 1500f, 900f, 0.45f, Wave.Square, 90f);
    public static AudioClip Notify => _notify ??= Tone("PC_Notify", 0.07f, 1046f, 1046f, 0.25f, Wave.Sine, 30f);
    public static AudioClip Success => _success ??= Arpeggio("PC_Success", new[] { 784f, 1175f, 1568f }, 0.06f, 0.35f);
    public static AudioClip Error => _error ??= Arpeggio("PC_Error", new[] { 220f, 165f }, 0.11f, 0.3f, Wave.Square);
    public static AudioClip Boot => _boot ??= Arpeggio("PC_Boot", new[] { 523f, 659f, 784f, 1046f }, 0.09f, 0.3f);
    public static AudioClip Shutdown => _shutdown ??= Arpeggio("PC_Shutdown", new[] { 880f, 659f, 440f }, 0.08f, 0.3f);
    public static AudioClip Hum => _hum ??= FanHum("PC_Hum", 2f);

    private enum Wave { Sine, Square }

    private static float Sample(Wave wave, float phase)
    {
        float s = Mathf.Sin(phase);
        // Soft square: punchy like an old PC speaker without being harsh.
        return wave == Wave.Square ? Mathf.Clamp(s * 3f, -1f, 1f) * 0.6f : s;
    }

    private static AudioClip Tone(string name, float duration, float startHz, float endHz, float volume, Wave wave, float decay)
    {
        int count = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[count];
        float phase = 0f;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;
            float hz = Mathf.Lerp(startHz, endHz, t);
            phase += 2f * Mathf.PI * hz / SampleRate;
            float env = Mathf.Exp(-decay * t * duration) * Mathf.Min(1f, i / 40f);
            data[i] = Sample(wave, phase) * env * volume;
        }
        return Make(name, data);
    }

    private static AudioClip Arpeggio(string name, float[] notes, float noteLength, float volume, Wave wave = Wave.Sine)
    {
        int perNote = Mathf.CeilToInt(SampleRate * noteLength);
        var data = new float[perNote * notes.Length + SampleRate / 10];
        for (int n = 0; n < notes.Length; n++)
        {
            float phase = 0f;
            // Let each note ring past the next one for a softer chime.
            int length = Mathf.Min(perNote * 2, data.Length - n * perNote);
            for (int i = 0; i < length; i++)
            {
                phase += 2f * Mathf.PI * notes[n] / SampleRate;
                float t = i / (float)SampleRate;
                float env = Mathf.Exp(-t * 18f) * Mathf.Min(1f, i / 60f);
                data[n * perNote + i] += Sample(wave, phase) * env * volume;
            }
        }
        return Make(name, data);
    }

    private static AudioClip FanHum(string name, float duration)
    {
        int count = Mathf.CeilToInt(SampleRate * duration);
        var data = new float[count];
        var rng = new System.Random(1337);
        float brown = 0f;
        for (int i = 0; i < count; i++)
        {
            // Brown noise for airflow plus a low mains hum.
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            brown = Mathf.Clamp(brown + white * 0.02f, -1f, 1f) * 0.998f;
            float t = i / (float)SampleRate;
            float hum = Mathf.Sin(2f * Mathf.PI * 50f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 100f * t) * 0.25f;
            data[i] = brown * 0.6f + hum * 0.15f;
        }
        // Crossfade the tail into the head and drop the tail, so the loop point is seamless.
        // The looped length is 1.9s, a whole number of 50Hz/100Hz cycles.
        int fade = SampleRate / 10;
        int loopLength = count - fade;
        var looped = new float[loopLength];
        for (int i = 0; i < loopLength; i++)
        {
            looped[i] = i < fade ? Mathf.Lerp(data[loopLength + i], data[i], i / (float)fade) : data[i];
        }
        return Make(name, looped);
    }

    private static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
