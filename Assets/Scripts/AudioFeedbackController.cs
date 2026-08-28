using UnityEngine;
using System.Collections;

/// <summary>
/// Sistema central de áudio do GarraMania.
/// Gera TODOS os efeitos sonoros proceduralmente via AudioClip.Create().
/// Funciona sem nenhum arquivo de áudio externo.
/// </summary>
public class AudioFeedbackController : MonoBehaviour
{
    public static AudioFeedbackController Instance { get; private set; }

    private AudioSource sfxSource;
    private AudioSource sfxSource2; // Segundo canal para evitar cortes
    private AudioSource musicSource;

    private AudioClip clipServo;
    private AudioClip clipClank;
    private AudioClip clipThud;
    private AudioClip clipFanfare;
    private AudioClip clipCoin;
    private AudioClip clipWarning;
    private AudioClip clipMusic;
    private AudioClip clipGrabSuccess;
    private AudioClip clipSlipStart;
    private AudioClip clipDropThud;
    private AudioClip clipDeliverySuccess;
    private AudioClip clipNearMiss;

    private float servoThrottle = 0f;
    private const float SERVO_COOLDOWN = 0.15f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f; // 2D

        sfxSource2 = gameObject.AddComponent<AudioSource>();
        sfxSource2.playOnAwake = false;
        sfxSource2.spatialBlend = 0f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.2f;
        musicSource.spatialBlend = 0f;

        GenerateAllClips();
    }

    void Start()
    {
        PlayMusic();
    }

    void Update()
    {
        if (servoThrottle > 0f) servoThrottle -= Time.deltaTime;
    }

    // ======================== API PÚBLICA ========================

    public void PlayServo()
    {
        if (servoThrottle > 0f || clipServo == null) return;
        servoThrottle = SERVO_COOLDOWN;
        sfxSource2.PlayOneShot(clipServo, 0.3f);
    }

    public void PlayClank()
    {
        if (clipClank != null) sfxSource.PlayOneShot(clipClank, 0.7f);
    }

    public void PlayThud()
    {
        if (clipThud != null) sfxSource.PlayOneShot(clipThud, 0.5f);
    }

    public void PlayFanfare()
    {
        if (clipFanfare != null) sfxSource.PlayOneShot(clipFanfare, 0.9f);
    }

    public void PlayCoin()
    {
        if (clipCoin != null) sfxSource.PlayOneShot(clipCoin, 0.6f);
    }

    public void PlayWarning()
    {
        if (clipWarning != null) sfxSource2.PlayOneShot(clipWarning, 0.5f);
    }

    public void PlayGrabSuccess()
    {
        if (clipGrabSuccess != null) sfxSource.PlayOneShot(clipGrabSuccess, 0.85f);
        else PlayClank();
    }

    public void PlaySlipStart()
    {
        if (clipSlipStart != null) sfxSource2.PlayOneShot(clipSlipStart, 0.65f);
        else PlayWarning();
    }

    public void PlayDropThud()
    {
        if (clipDropThud != null) sfxSource.PlayOneShot(clipDropThud, 0.75f);
        else PlayThud();
    }

    public void PlayDeliverySuccess()
    {
        if (clipDeliverySuccess != null) sfxSource.PlayOneShot(clipDeliverySuccess, 0.95f);
        else PlayFanfare();
    }

    public void PlayNearMiss()
    {
        if (clipNearMiss != null) sfxSource2.PlayOneShot(clipNearMiss, 0.6f);
        else PlayWarning();
    }

    public void PlayMusic()
    {
        if (clipMusic != null && !musicSource.isPlaying)
        {
            musicSource.clip = clipMusic;
            musicSource.Play();
        }
    }

    public void StopMusic()
    {
        musicSource.Stop();
    }

    // ======================== GERAÇÃO PROCEDURAL ========================

    void GenerateAllClips()
    {
        int sampleRate = 44100;

        clipServo = GenerateServo(sampleRate);
        clipClank = GenerateClank(sampleRate);
        clipThud = GenerateThud(sampleRate);
        clipFanfare = GenerateFanfare(sampleRate);
        clipCoin = GenerateCoin(sampleRate);
        clipWarning = GenerateWarning(sampleRate);
        clipMusic = GenerateArcadeLoop(sampleRate);

        clipGrabSuccess = GenerateGrabSuccess(sampleRate);
        clipSlipStart = GenerateSlipStart(sampleRate);
        clipDropThud = GenerateDropThud(sampleRate);
        clipDeliverySuccess = GenerateDeliverySuccess(sampleRate);
        clipNearMiss = GenerateNearMiss(sampleRate);

        Debug.Log("[AudioFeedback] Todos os clips de áudio gerados proceduralmente!");
    }

    /// <summary>Servo motor: buzz baixo com modulação</summary>
    AudioClip GenerateServo(int sr)
    {
        int samples = (int)(sr * 0.12f); // 120ms
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float envelope = 1f - ((float)i / samples); // Fade out
            data[i] = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.3f * envelope;
            data[i] += Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.15f * envelope;
            data[i] += Random.Range(-0.05f, 0.05f) * envelope; // Noise
        }
        return CreateClip("Servo", samples, sr, data);
    }

    /// <summary>Clank metálico: impacto curto multi-frequência</summary>
    AudioClip GenerateClank(int sr)
    {
        int samples = (int)(sr * 0.15f); // 150ms
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 30f); // Decay rápido
            data[i] = Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.4f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.3f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.15f * env;
            data[i] += Random.Range(-0.1f, 0.1f) * env * 0.5f;
        }
        return CreateClip("Clank", samples, sr, data);
    }

    /// <summary>Thud suave: impacto grave abafado</summary>
    AudioClip GenerateThud(int sr)
    {
        int samples = (int)(sr * 0.2f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 15f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 80f * t) * 0.6f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 60f * t) * 0.3f * env;
        }
        return CreateClip("Thud", samples, sr, data);
    }

    /// <summary>Fanfarra: 4 notas ascendentes triunfais (Dó-Mi-Sol-Dó)</summary>
    AudioClip GenerateFanfare(int sr)
    {
        float duration = 0.8f;
        int samples = (int)(sr * duration);
        float[] data = new float[samples];

        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
        float noteLength = duration / freqs.Length;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            int noteIndex = Mathf.Min((int)(t / noteLength), freqs.Length - 1);
            float noteT = t - (noteIndex * noteLength);
            float env = Mathf.Clamp01(1f - (noteT / noteLength) * 0.5f); // Sustain com leve fade
            float attack = Mathf.Clamp01(noteT * 50f); // Attack rápido

            data[i] = Mathf.Sin(2f * Mathf.PI * freqs[noteIndex] * t) * 0.4f * env * attack;
            data[i] += Mathf.Sin(2f * Mathf.PI * freqs[noteIndex] * 2f * t) * 0.15f * env * attack; // Harmônico
        }
        return CreateClip("Fanfare", samples, sr, data);
    }

    /// <summary>Moeda caindo: ding metálico agudo com reverb</summary>
    AudioClip GenerateCoin(int sr)
    {
        int samples = (int)(sr * 0.3f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 8f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 1400f * t) * 0.35f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 1800f * t) * 0.25f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 2800f * t) * 0.1f * Mathf.Exp(-t * 15f);
        }
        return CreateClip("Coin", samples, sr, data);
    }

    /// <summary>Warning: beep curto de alerta</summary>
    AudioClip GenerateWarning(int sr)
    {
        int samples = (int)(sr * 0.1f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = (i < samples / 2) ? 1f : (1f - (float)(i - samples / 2) / (samples / 2));
            data[i] = Mathf.Sin(2f * Mathf.PI * 880f * t) * 0.5f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 1760f * t) * 0.2f * env;
        }
        return CreateClip("Warning", samples, sr, data);
    }

    /// <summary>Música de fundo: loop chiptune arcade simples (16 compassos)</summary>
    AudioClip GenerateArcadeLoop(int sr)
    {
        float bpm = 130f;
        float beatDuration = 60f / bpm;
        int totalBeats = 32; // 8 compassos de 4/4
        float totalDuration = totalBeats * beatDuration;
        int samples = (int)(sr * totalDuration);
        float[] data = new float[samples];

        // Progressão de acordes: I - V - vi - IV (pop progression)
        float[] bassNotes = {
            130.81f, 130.81f, 130.81f, 130.81f, // C3 (4 beats)
            196.00f, 196.00f, 196.00f, 196.00f, // G3
            220.00f, 220.00f, 220.00f, 220.00f, // A3 (Am)
            174.61f, 174.61f, 174.61f, 174.61f, // F3
            130.81f, 130.81f, 130.81f, 130.81f, // C3
            196.00f, 196.00f, 196.00f, 196.00f, // G3
            220.00f, 220.00f, 220.00f, 220.00f, // A3
            174.61f, 174.61f, 174.61f, 174.61f  // F3
        };

        // Melodia simples (notas por beat)
        float[] melody = {
            523.25f, 587.33f, 659.25f, 523.25f, // C5 D5 E5 C5
            783.99f, 698.46f, 659.25f, 587.33f, // G5 F5 E5 D5
            880.00f, 783.99f, 659.25f, 783.99f, // A5 G5 E5 G5
            698.46f, 659.25f, 587.33f, 523.25f, // F5 E5 D5 C5
            523.25f, 0f,      659.25f, 0f,      // C5 - E5 -
            783.99f, 0f,      659.25f, 587.33f, // G5 - E5 D5
            880.00f, 783.99f, 659.25f, 523.25f, // A5 G5 E5 C5
            587.33f, 523.25f, 0f,      0f       // D5 C5 - -
        };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            int beat = (int)(t / beatDuration) % totalBeats;
            float beatT = t % beatDuration;

            // Bass (onda quadrada suave)
            float bassFreq = bassNotes[beat];
            float bassEnv = Mathf.Clamp01(1f - (beatT / beatDuration) * 0.3f);
            float bass = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * bassFreq * t)) * 0.08f * bassEnv;

            // Melodia (onda triangular)
            float melFreq = melody[beat];
            float melEnv = Mathf.Clamp01(1f - (beatT / beatDuration) * 0.6f) * Mathf.Clamp01(beatT * 30f);
            float mel = 0f;
            if (melFreq > 0f)
            {
                float phase = (melFreq * t) % 1f;
                mel = (phase < 0.5f ? (4f * phase - 1f) : (3f - 4f * phase)) * 0.1f * melEnv;
            }

            // Hi-hat (noise rítmico)
            float hihatEnv = Mathf.Exp(-beatT * 40f) * ((beat % 2 == 0) ? 0.06f : 0.03f);
            float hihat = Random.Range(-1f, 1f) * hihatEnv;

            data[i] = Mathf.Clamp(bass + mel + hihat, -1f, 1f);
        }

        return CreateClip("ArcadeLoop", samples, sr, data);
    }

    /// <summary>Engate bem-sucedido: trava mecânica com clique ressonante e brilho</summary>
    AudioClip GenerateGrabSuccess(int sr)
    {
        int samples = (int)(sr * 0.28f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float clickEnv = Mathf.Exp(-t * 45f);
            float ringEnv = Mathf.Exp(-t * 12f);
            data[i] = (Mathf.Sin(2f * Mathf.PI * 920f * t) * 0.4f + Random.Range(-0.1f, 0.1f)) * clickEnv;
            data[i] += (Mathf.Sin(2f * Mathf.PI * 1350f * t) * 0.35f + Mathf.Sin(2f * Mathf.PI * 2700f * t) * 0.2f) * ringEnv;
        }
        return CreateClip("GrabSuccess", samples, sr, data);
    }

    /// <summary>Início de escorregamento: atrito tenso / chiado mecânico de alta tensão</summary>
    AudioClip GenerateSlipStart(int sr)
    {
        int samples = (int)(sr * 0.20f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.20f) * Mathf.PI);
            float freq = Mathf.Lerp(650f, 1350f, t / 0.20f) + Mathf.Sin(2f * Mathf.PI * 45f * t) * 120f;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f * env;
            data[i] += Random.Range(-0.15f, 0.15f) * env * 0.5f;
        }
        return CreateClip("SlipStart", samples, sr, data);
    }

    /// <summary>Queda do prêmio: baque grave e abafado da pelúcia caindo no monte</summary>
    AudioClip GenerateDropThud(int sr)
    {
        int samples = (int)(sr * 0.32f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 12f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 65f * t) * 0.7f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.3f * env;
            data[i] += Random.Range(-0.08f, 0.08f) * Mathf.Exp(-t * 35f);
        }
        return CreateClip("DropThud", samples, sr, data);
    }

    /// <summary>Entrega bem-sucedida: fanfarra triunfal arcade de vitória</summary>
    AudioClip GenerateDeliverySuccess(int sr)
    {
        float duration = 1.0f;
        int samples = (int)(sr * duration);
        float[] data = new float[samples];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f }; // C5, E5, G5, C6, E6
        float noteDur = duration / notes.Length;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            int nIdx = Mathf.Min((int)(t / noteDur), notes.Length - 1);
            float nT = t - (nIdx * noteDur);
            float env = Mathf.Clamp01(1f - (nT / noteDur) * 0.45f) * Mathf.Clamp01(nT * 60f);
            data[i] = Mathf.Sin(2f * Mathf.PI * notes[nIdx] * t) * 0.35f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * notes[nIdx] * 2f * t) * 0.15f * env;
            // Sparkle chime
            data[i] += Mathf.Sin(2f * Mathf.PI * (notes[nIdx] * 3f) * t) * 0.08f * env;
        }
        return CreateClip("DeliverySuccess", samples, sr, data);
    }

    /// <summary>Quase pegou: zumbido descendente de tensão / near-miss</summary>
    AudioClip GenerateNearMiss(int sr)
    {
        int samples = (int)(sr * 0.22f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = 1f - (t / 0.22f);
            float freq = Mathf.Lerp(520f, 260f, t / 0.22f);
            data[i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.25f * env;
        }
        return CreateClip("NearMiss", samples, sr, data);
    }

    AudioClip CreateClip(string name, int samples, int sampleRate, float[] data)
    {
        AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
