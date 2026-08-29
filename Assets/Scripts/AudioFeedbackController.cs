using System.Collections;
using UnityEngine;

/// <summary>
/// Sistema central de áudio do GarraMania (Arcade Audio Master).
/// Gera proceduralmente sons realistas de motor elétrico, solenoide mecânico,
/// batidas de acrílico, fanfarras e trilha de tensão nos 10s finais.
/// </summary>
public class AudioFeedbackController : MonoBehaviour
{
    public static AudioFeedbackController Instance { get; private set; }

    private AudioSource sfxSource;
    private AudioSource sfxSource2;
    private AudioSource motorHumSource;
    private AudioSource musicSource;
    private AudioSource tensionSource;

    private AudioClip clipServo;
    private AudioClip clipMotorHumLoop;
    private AudioClip clipClank;
    private AudioClip clipSolenoidClamp;
    private AudioClip clipSolenoidRelease;
    private AudioClip clipThud;
    private AudioClip clipFanfare;
    private AudioClip clipCoin;
    private AudioClip clipWarning;
    private AudioClip clipTensionTick;
    private AudioClip clipMusic;
    private AudioClip clipGrabSuccess;
    private AudioClip clipSlipStart;
    private AudioClip clipDropThud;
    private AudioClip clipDeliverySuccess;
    private AudioClip clipNearMiss;

    private float servoThrottle = 0f;
    private const float SERVO_COOLDOWN = 0.15f;
    private bool isMotorMoving = false;
    private float targetMotorVol = 0f;
    private float currentMotorVol = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        sfxSource2 = gameObject.AddComponent<AudioSource>();
        sfxSource2.playOnAwake = false;
        sfxSource2.spatialBlend = 0f;

        motorHumSource = gameObject.AddComponent<AudioSource>();
        motorHumSource.playOnAwake = false;
        motorHumSource.loop = true;
        motorHumSource.volume = 0f;
        motorHumSource.spatialBlend = 0f;

        tensionSource = gameObject.AddComponent<AudioSource>();
        tensionSource.playOnAwake = false;
        tensionSource.loop = false;
        tensionSource.spatialBlend = 0f;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.18f;
        musicSource.spatialBlend = 0f;

        GenerateAllClips();

        if (clipMotorHumLoop != null)
        {
            motorHumSource.clip = clipMotorHumLoop;
            motorHumSource.Play();
        }
    }

    void Start()
    {
        PlayMusic();
    }

    void Update()
    {
        if (servoThrottle > 0f) servoThrottle -= Time.deltaTime;

        // Suavização do zumbido do motor elétrico
        targetMotorVol = isMotorMoving ? 0.35f : 0f;
        currentMotorVol = Mathf.MoveTowards(currentMotorVol, targetMotorVol, Time.deltaTime * 3.5f);
        if (motorHumSource != null) motorHumSource.volume = currentMotorVol;
    }

    // ======================== API PÚBLICA DE CONTROLE ========================

    public void SetMotorMoving(bool moving)
    {
        isMotorMoving = moving;
    }

    public void PlayServo()
    {
        if (servoThrottle > 0f || clipServo == null) return;
        servoThrottle = SERVO_COOLDOWN;
        sfxSource2.PlayOneShot(clipServo, 0.3f);
    }

    /// <summary>Estalo forte e metálico do solenoide elétrico ao fechar com pressão total</summary>
    public void PlaySolenoidClamp()
    {
        if (clipSolenoidClamp != null) sfxSource.PlayOneShot(clipSolenoidClamp, 0.95f);
        else PlayClank();
    }

    /// <summary>Liberação pneumática da garra abrindo sobre a calha de entrega</summary>
    public void PlaySolenoidRelease()
    {
        if (clipSolenoidRelease != null) sfxSource.PlayOneShot(clipSolenoidRelease, 0.85f);
        else PlayClank();
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
        if (clipCoin != null) sfxSource.PlayOneShot(clipCoin, 0.65f);
    }

    public void PlayWarning()
    {
        if (clipWarning != null) sfxSource2.PlayOneShot(clipWarning, 0.5f);
    }

    public void PlayTensionTick()
    {
        if (clipTensionTick != null) tensionSource.PlayOneShot(clipTensionTick, 0.7f);
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

    // ======================== SÍNTESE PROCEDURAL DE ÁUDIO ========================

    void GenerateAllClips()
    {
        int sampleRate = 44100;

        clipServo = GenerateServo(sampleRate);
        clipMotorHumLoop = GenerateMotorHumLoop(sampleRate);
        clipClank = GenerateClank(sampleRate);
        clipSolenoidClamp = GenerateSolenoidClamp(sampleRate);
        clipSolenoidRelease = GenerateSolenoidRelease(sampleRate);
        clipThud = GenerateThud(sampleRate);
        clipFanfare = GenerateFanfare(sampleRate);
        clipCoin = GenerateCoin(sampleRate);
        clipWarning = GenerateWarning(sampleRate);
        clipTensionTick = GenerateTensionTick(sampleRate);
        clipMusic = GenerateArcadeLoop(sampleRate);

        clipGrabSuccess = GenerateGrabSuccess(sampleRate);
        clipSlipStart = GenerateSlipStart(sampleRate);
        clipDropThud = GenerateDropThud(sampleRate);
        clipDeliverySuccess = GenerateDeliverySuccess(sampleRate);
        clipNearMiss = GenerateNearMiss(sampleRate);

        Debug.Log("[AudioFeedbackController] Todos os 14 clips de áudio gerados com síntese de alta fidelidade!");
    }

    /// <summary>Loop contínuo de motor de passo elétrico do trole de teto</summary>
    AudioClip GenerateMotorHumLoop(int sr)
    {
        float duration = 1.0f;
        int samples = (int)(sr * duration);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            // Frequência fundamental de motor industrial 160Hz com harmônicos
            float wave = Mathf.Sin(2f * Mathf.PI * 160f * t) * 0.35f;
            wave += Mathf.Sin(2f * Mathf.PI * 320f * t) * 0.20f;
            wave += Mathf.Sin(2f * Mathf.PI * 480f * t) * 0.10f;
            // Ruído de engrenagens
            wave += Random.Range(-0.06f, 0.06f);
            data[i] = wave * 0.4f;
        }

        return CreateClip("MotorHumLoop", samples, sr, data);
    }

    /// <summary>Estalo potente e seco do solenoide fechando com impacto mecânico</summary>
    AudioClip GenerateSolenoidClamp(int sr)
    {
        int samples = (int)(sr * 0.22f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float envAttack = Mathf.Exp(-t * 55f);
            float envRing = Mathf.Exp(-t * 18f);

            // Impacto grave seco (punch)
            float lowPunch = Mathf.Sin(2f * Mathf.PI * 110f * t) * 0.7f * envAttack;
            // Estalo metálico agudo de lâmina de aço
            float highClick = (Mathf.Sin(2f * Mathf.PI * 1450f * t) * 0.4f + Mathf.Sin(2f * Mathf.PI * 2600f * t) * 0.25f) * envRing;
            float noise = Random.Range(-0.15f, 0.15f) * envAttack;

            data[i] = Mathf.Clamp(lowPunch + highClick + noise, -1f, 1f);
        }

        return CreateClip("SolenoidClamp", samples, sr, data);
    }

    /// <summary>Abertura do solenoide com liberação mecânica</summary>
    AudioClip GenerateSolenoidRelease(int sr)
    {
        int samples = (int)(sr * 0.18f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 30f);
            float wave = Mathf.Sin(2f * Mathf.PI * 720f * t) * 0.35f * env;
            wave += Mathf.Sin(2f * Mathf.PI * 1100f * t) * 0.25f * env;
            wave += Random.Range(-0.08f, 0.08f) * env;
            data[i] = wave;
        }

        return CreateClip("SolenoidRelease", samples, sr, data);
    }

    /// <summary>Tique-taque de tensão nos 10s finais</summary>
    AudioClip GenerateTensionTick(int sr)
    {
        int samples = (int)(sr * 0.08f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 80f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 1250f * t) * 0.5f * env;
        }

        return CreateClip("TensionTick", samples, sr, data);
    }

    AudioClip GenerateServo(int sr)
    {
        int samples = (int)(sr * 0.12f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float envelope = 1f - ((float)i / samples);
            data[i] = Mathf.Sin(2f * Mathf.PI * 120f * t) * 0.3f * envelope;
            data[i] += Mathf.Sin(2f * Mathf.PI * 180f * t) * 0.15f * envelope;
            data[i] += Random.Range(-0.05f, 0.05f) * envelope;
        }
        return CreateClip("Servo", samples, sr, data);
    }

    AudioClip GenerateClank(int sr)
    {
        int samples = (int)(sr * 0.15f);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            float env = Mathf.Exp(-t * 30f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 800f * t) * 0.4f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.3f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * 3200f * t) * 0.15f * env;
            data[i] += Random.Range(-0.1f, 0.1f) * env * 0.5f;
        }
        return CreateClip("Clank", samples, sr, data);
    }

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

    AudioClip GenerateFanfare(int sr)
    {
        float duration = 0.8f;
        int samples = (int)(sr * duration);
        float[] data = new float[samples];
        float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
        float noteLength = duration / freqs.Length;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            int noteIndex = Mathf.Min((int)(t / noteLength), freqs.Length - 1);
            float noteT = t - (noteIndex * noteLength);
            float env = Mathf.Clamp01(1f - (noteT / noteLength) * 0.5f);
            float attack = Mathf.Clamp01(noteT * 50f);

            data[i] = Mathf.Sin(2f * Mathf.PI * freqs[noteIndex] * t) * 0.4f * env * attack;
            data[i] += Mathf.Sin(2f * Mathf.PI * freqs[noteIndex] * 2f * t) * 0.15f * env * attack;
        }
        return CreateClip("Fanfare", samples, sr, data);
    }

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

    AudioClip GenerateArcadeLoop(int sr)
    {
        float bpm = 130f;
        float beatDuration = 60f / bpm;
        int totalBeats = 32;
        float totalDuration = totalBeats * beatDuration;
        int samples = (int)(sr * totalDuration);
        float[] data = new float[samples];

        float[] bassNotes = {
            130.81f, 130.81f, 130.81f, 130.81f,
            196.00f, 196.00f, 196.00f, 196.00f,
            220.00f, 220.00f, 220.00f, 220.00f,
            174.61f, 174.61f, 174.61f, 174.61f,
            130.81f, 130.81f, 130.81f, 130.81f,
            196.00f, 196.00f, 196.00f, 196.00f,
            220.00f, 220.00f, 220.00f, 220.00f,
            174.61f, 174.61f, 174.61f, 174.61f
        };

        float[] melody = {
            523.25f, 587.33f, 659.25f, 523.25f,
            783.99f, 698.46f, 659.25f, 587.33f,
            880.00f, 783.99f, 659.25f, 783.99f,
            698.46f, 659.25f, 587.33f, 523.25f,
            523.25f, 0f,      659.25f, 0f,
            783.99f, 0f,      659.25f, 587.33f,
            880.00f, 783.99f, 659.25f, 523.25f,
            587.33f, 523.25f, 0f,      0f
        };

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            int beat = (int)(t / beatDuration) % totalBeats;
            float beatT = t % beatDuration;

            float bassFreq = bassNotes[beat];
            float bassEnv = Mathf.Clamp01(1f - (beatT / beatDuration) * 0.3f);
            float bass = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * bassFreq * t)) * 0.08f * bassEnv;

            float melFreq = melody[beat];
            float melEnv = Mathf.Clamp01(1f - (beatT / beatDuration) * 0.6f) * Mathf.Clamp01(beatT * 30f);
            float mel = 0f;
            if (melFreq > 0f)
            {
                float phase = (melFreq * t) % 1f;
                mel = (phase < 0.5f ? (4f * phase - 1f) : (3f - 4f * phase)) * 0.1f * melEnv;
            }

            float hihatEnv = Mathf.Exp(-beatT * 40f) * ((beat % 2 == 0) ? 0.06f : 0.03f);
            float hihat = Random.Range(-1f, 1f) * hihatEnv;

            data[i] = Mathf.Clamp(bass + mel + hihat, -1f, 1f);
        }

        return CreateClip("ArcadeLoop", samples, sr, data);
    }

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

    AudioClip GenerateDeliverySuccess(int sr)
    {
        float duration = 1.0f;
        int samples = (int)(sr * duration);
        float[] data = new float[samples];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f };
        float noteDur = duration / notes.Length;

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sr;
            int nIdx = Mathf.Min((int)(t / noteDur), notes.Length - 1);
            float nT = t - (nIdx * noteDur);
            float env = Mathf.Clamp01(1f - (nT / noteDur) * 0.45f) * Mathf.Clamp01(nT * 60f);
            data[i] = Mathf.Sin(2f * Mathf.PI * notes[nIdx] * t) * 0.35f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * notes[nIdx] * 2f * t) * 0.15f * env;
            data[i] += Mathf.Sin(2f * Mathf.PI * (notes[nIdx] * 3f) * t) * 0.08f * env;
        }
        return CreateClip("DeliverySuccess", samples, sr, data);
    }

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
