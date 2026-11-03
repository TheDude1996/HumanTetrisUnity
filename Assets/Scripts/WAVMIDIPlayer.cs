using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class MidiWavPlayer : MonoBehaviour
{
    [Header("Dateinamen (in Assets/StreamingAssets)")]
    public string midiFileName = "";
    public string wavFileName = "";

    //[Header("MIDI Geschwindigkeit")]
    //[Range(0.25f, 2f)]
    private float midiSpeed = 1f;

    [Header("Referenzen")]
    [Tooltip("Dient als Vorlage (Volume, Mixer, Spatial Blend ...) für alle Pool-Voices")]
    public AudioSource audioSource;

    [Header("Loop Einstellungen")]
    public bool loopMidi = true;

    [Header("Wiedergabe")]
    [Tooltip("Anzahl gleichzeitig klingender Noten")]
    public int voiceCount = 16;
    [Tooltip("Wie weit (Sekunden) Noten im Voraus auf der DSP-Uhr eingeplant werden")]
    public double scheduleAhead = 0.2;
    [Tooltip("Startverzögerung, damit die ersten Noten sauber eingeplant werden können")]
    public double startDelay = 0.1;

    [Header("Sample-Aufbereitung")]
    [Tooltip("Lautstärke-Schwelle (Anteil vom lautesten Abschnitt), ab der die Aufnahme als 'Ton' zählt")]
    [Range(0.01f, 0.9f)]
    public float soundThreshold = 0.15f;
    [Tooltip("Anteil der Notenlänge, der voll klingt (1 = gebunden, kleiner = abgesetzter). Danach folgt das Ausklingen.")]
    [Range(0.3f, 1f)]
    public float noteLength = 1f;
    [Tooltip("Ausklingzeit am Ende jeder Note in Sekunden (Release)")]
    [Range(0f, 1f)]
    public float releaseTime = 0.15f;

    [Header("Sustain (Schleife für lange Noten)")]
    [Tooltip("Ab hier (Sekunden in der aufbereiteten Aufnahme) wird für lange Noten ein Stück in Schleife wiederholt")]
    public float sustainLoopStart = 0.25f;
    [Tooltip("Länge des wiederholten Stücks in Sekunden")]
    public float sustainLoopLength = 0.3f;
    [Tooltip("Überblendung an der Schleifenstelle in Sekunden")]
    public float sustainCrossfade = 0.06f;

    [Header("Begleitung (synthetischer 8-Bit-Sound)")]
    [Tooltip("Spielt die Melodie zusätzlich als Rechteckwelle und eine Basslinie als Dreieckwelle, taktgenau zur Stimme")]
    public bool playBacking = true;
    [Tooltip("Lautstärke der Begleit-Melodie (Rechteckwelle) relativ zur Stimme (0.5 = 50 %)")]
    [Range(0f, 1f)]
    public float backingVolume = 0.5f;
    [Tooltip("Lautstärke der Basslinie relativ zur Stimme (0.5 = 50 %)")]
    [Range(0f, 1f)]
    public float bassVolume = 0.5f;

    // Basslinie: Grundton je Takt (MIDI-Note) zur Akkordfolge von Korobeiniki, Teil A + B (passt zu Korobeiniki.mid)
    // Teil A: E | Am | E | Am | Dm | Am | E | Am    Teil B: Am | E | Am | E | Am | E | Am | E
    private static readonly int[] bassRoots = { 40, 45, 40, 45, 38, 45, 40, 45, 45, 40, 45, 40, 45, 40, 45, 40 };

    private MidiFile midiFile;
    private AudioClip sampleClip;

    // Eine vorberechnete Note: Startzeit relativ zum Durchlaufbeginn, Tonhöhe und passend zugeschnittenes Sample
    private struct ScheduledNote
    {
        public double time;
        public float pitch;
        public AudioClip clip;
        public float volume;
    }

    private float baseVolume = 1f;
    private readonly Dictionary<string, AudioClip> synthClips = new Dictionary<string, AudioClip>();

    // Aufbereitete Aufnahme (mono) und je Tonhöhe/Länge zugeschnittene Clips
    private float[] sampleData;
    private int sampleFrequency;
    private readonly Dictionary<long, AudioClip> noteClips = new Dictionary<long, AudioClip>();

    private List<ScheduledNote> notes;
    private double loopDuration;   // Länge eines Durchlaufs (Sekunden)
    private int nextNoteIndex;
    private double passStartDsp;   // DSP-Zeit, zu der der aktuelle Durchlauf beginnt
    private bool isPlaying;

    private AudioSource[] voices;
    private double[] voiceEndDsp;  // wann die jeweilige Voice wieder frei ist

    void Start()
    {
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        if (string.IsNullOrEmpty(midiFileName) || string.IsNullOrEmpty(wavFileName))
        {
            Debug.LogError("Bitte trage die Dateinamen im Inspector ein!");
            return;
        }

        StartCoroutine(LoadWavAndMidi());
    }

    IEnumerator LoadWavAndMidi()
    {
        // --- 1. WAV LADEN ---
        // Vorrang: Aufnahme des gewählten Spielers im Netzwerkordner, sonst die Datei aus StreamingAssets
        string cleanWavPath = Path.Combine(Application.streamingAssetsPath, wavFileName).Replace("file://", "");
        string playerSample = PlayerDataManager.Instance.SelectedSamplePath;
        if (playerSample != null && File.Exists(playerSample))
        {
            cleanWavPath = playerSample;
            Debug.Log("Nutze Aufnahme des Spielers: " + playerSample);
        }

        if (!File.Exists(cleanWavPath))
        {
            Debug.LogError("WAV Datei nicht gefunden: " + cleanWavPath);
            yield break;
        }

        byte[] wavBytes = File.ReadAllBytes(cleanWavPath);
        sampleClip = WavUtility.ToAudioClip(wavBytes);

        // Fallback falls die Byte-Methode nicht geht
        if (sampleClip == null)
        {
            sampleClip = WavUtility.ToAudioClip("file://" + cleanWavPath);
        }

        if (sampleClip == null)
        {
            Debug.LogError("Fehler: Konnte WAV nicht decodieren. Ist es eine 16-Bit PCM WAV?");
            yield break;
        }

        sampleClip = PrepareSample(sampleClip);
        Debug.Log("WAV geladen: " + wavFileName + " (aufbereitet: " + sampleClip.length.ToString("0.00") + " s)");
        yield return null;

        // --- 2. MIDI LADEN ---
        string cleanMidiPath = Path.Combine(Application.streamingAssetsPath, midiFileName).Replace("file://", "");

        if (!File.Exists(cleanMidiPath))
        {
            Debug.LogError("MIDI Datei nicht gefunden: " + cleanMidiPath);
            yield break;
        }

        try
        {
            using (var stream = new MemoryStream(File.ReadAllBytes(cleanMidiPath)))
            {
                midiFile = MidiFile.Read(stream);
            }
            Debug.Log("MIDI geladen: " + midiFileName);
        }
        catch (System.Exception e)
        {
            Debug.LogError("Fehler beim Lesen der MIDI: " + e.Message);
            yield break;
        }

        Debug.Log("Alle Dateien geladen. Starte Wiedergabe...");
        PlayMidiWithSample();
    }

    void PlayMidiWithSample()
    {
        if (midiFile == null || sampleClip == null)
            return;

        CreateVoicePool();

        sampleFrequency = sampleClip.frequency;
        sampleData = new float[sampleClip.samples];
        sampleClip.GetData(sampleData, 0);

        // Alle Noten einmal vorberechnen (TempoMap nur einmal holen) und nach Zeit sortieren
        TempoMap tempoMap = midiFile.GetTempoMap();
        double speed = Mathf.Max(0.01f, midiSpeed);

        notes = new List<ScheduledNote>();
        loopDuration = 0;

        foreach (var note in midiFile.GetNotes())
        {
            double start = note.TimeAs<MetricTimeSpan>(tempoMap).TotalSeconds / speed;
            double end = TimeConverter.ConvertTo<MetricTimeSpan>(note.EndTime, tempoMap).TotalSeconds / speed;

            float pitch = GetPitchFromMidiNumber(note.NoteNumber);
            notes.Add(new ScheduledNote
            {
                time = start,
                pitch = pitch,
                clip = GetNoteClip(note.NoteNumber, end - start, pitch),
                volume = 1f
            });

            // Begleitung: dieselbe Note als Rechteckwelle (synthetisch in exakter Tonhöhe -> Pitch 1)
            if (playBacking && backingVolume > 0)
            {
                notes.Add(new ScheduledNote
                {
                    time = start,
                    pitch = 1f,
                    clip = GetSynthClip(note.NoteNumber, end - start, false),
                    volume = backingVolume
                });
            }

            if (end > loopDuration) loopDuration = end;
        }

        if (playBacking && bassVolume > 0)
            AddBassLine(tempoMap, speed);

        notes.Sort((a, b) => a.time.CompareTo(b.time));

        if (notes.Count == 0) return;

        nextNoteIndex = 0;
        passStartDsp = AudioSettings.dspTime + startDelay;
        isPlaying = true;
    }

    void CreateVoicePool()
    {
        voices = new AudioSource[Mathf.Max(1, voiceCount)];
        voiceEndDsp = new double[voices.Length];

        for (int i = 0; i < voices.Length; i++)
        {
            var go = new GameObject("Voice " + i);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.clip = sampleClip;

            // Einstellungen der Vorlage übernehmen
            if (audioSource != null)
            {
                baseVolume = audioSource.volume;
                src.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
                src.volume = audioSource.volume;
                src.spatialBlend = audioSource.spatialBlend;
                src.priority = audioSource.priority;
                src.panStereo = audioSource.panStereo;
            }

            voices[i] = src;
        }
    }

    void Update()
    {
        if (!isPlaying) return;

        double now = AudioSettings.dspTime;

        // Alle Noten, die innerhalb des Lookahead-Fensters liegen, sample-genau einplanen
        while (isPlaying && passStartDsp + notes[nextNoteIndex].time < now + scheduleAhead)
        {
            ScheduledNote n = notes[nextNoteIndex++];

            if (n.pitch > 0)
            {
                double startDsp = passStartDsp + n.time;
                if (startDsp < now) startDsp = now; // z.B. nach einem Hänger nicht in der Vergangenheit planen

                int v = GetFreeVoice(startDsp);
                AudioSource src = voices[v];
                // Clip und Pitch nur auf DIESER Voice setzen -> andere klingende Noten bleiben unverändert.
                // Pitch = Abspielgeschwindigkeit: schneller abspielen -> höherer Ton.
                src.clip = n.clip;
                src.pitch = n.pitch;
                src.volume = baseVolume * n.volume;
                src.PlayScheduled(startDsp);
                voiceEndDsp[v] = startDsp + n.clip.length / n.pitch;
            }

            // Ende eines Durchlaufs: nächsten Durchlauf nahtlos anschließen oder aufhören
            if (nextNoteIndex >= notes.Count)
            {
                if (loopMidi && loopDuration > 0)
                {
                    nextNoteIndex = 0;
                    passStartDsp += loopDuration;
                }
                else
                {
                    isPlaying = false;
                }
            }
        }
    }

    // Freie Voice suchen; sind alle belegt, die zuerst endende stehlen
    int GetFreeVoice(double startDsp)
    {
        int best = 0;
        for (int i = 0; i < voices.Length; i++)
        {
            if (voiceEndDsp[i] <= startDsp) return i;
            if (voiceEndDsp[i] < voiceEndDsp[best]) best = i;
        }
        return best;
    }

    // Aufnahme für die Melodie vorbereiten:
    // - Knackser und Stille am Anfang entfernen (sonst setzt jede Note verspätet ein),
    //   dazu wird der längste zusammenhängende laute Abschnitt gesucht
    // - normalisieren (Zuschneiden pro Note passiert in GetNoteClip)
    AudioClip PrepareSample(AudioClip clip)
    {
        int channels = clip.channels;
        int freq = clip.frequency;
        int sampleCount = clip.samples;

        float[] data = new float[sampleCount * channels];
        clip.GetData(data, 0);

        // Auf Mono mischen
        float[] mono = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float sum = 0;
            for (int c = 0; c < channels; c++) sum += data[i * channels + c];
            mono[i] = sum / channels;
        }

        // Lautstärke in 10-ms-Fenstern
        int win = Mathf.Max(1, freq / 100);
        int winCount = sampleCount / win;
        if (winCount < 3) return clip;

        float[] rms = new float[winCount];
        float maxRms = 0;
        for (int w = 0; w < winCount; w++)
        {
            double acc = 0;
            for (int k = w * win; k < (w + 1) * win; k++) acc += mono[k] * mono[k];
            rms[w] = Mathf.Sqrt((float)(acc / win));
            if (rms[w] > maxRms) maxRms = rms[w];
        }
        if (maxRms <= 0) return clip;

        // Längsten lauten Abschnitt finden (Lücken bis 50 ms gehören noch dazu)
        float threshold = maxRms * soundThreshold;
        int bestStart = 0, bestLength = 0, currentStart = -1, gap = 0;
        for (int w = 0; w < winCount; w++)
        {
            if (rms[w] >= threshold)
            {
                if (currentStart < 0) currentStart = w;
                gap = 0;
                int length = w - currentStart + 1;
                if (length > bestLength)
                {
                    bestLength = length;
                    bestStart = currentStart;
                }
            }
            else if (currentStart >= 0 && ++gap > 5)
            {
                currentStart = -1;
                gap = 0;
            }
        }

        int start = bestStart * win;
        int count = bestLength * win;
        if (count < win * 3) return clip;

        float[] result = new float[count];
        float peak = 0;
        for (int i = 0; i < count; i++)
        {
            result[i] = mono[start + i];
            peak = Mathf.Max(peak, Mathf.Abs(result[i]));
        }

        // Normalisieren (Ein-/Ausblenden passiert pro Note in GetNoteClip)
        float gain = peak > 0 ? 0.9f / peak : 1f;
        for (int i = 0; i < count; i++) result[i] *= gain;

        AudioClip prepared = AudioClip.Create(clip.name + "_prepared", count, 1, freq, false);
        prepared.SetData(result, 0);
        return prepared;
    }

    // Sample für eine Note bauen (wie ein einfacher Sampler: Attack -> Sustain-Schleife -> Release).
    // Die Tonhöhe entsteht durch schnelleres/langsameres Abspielen (AudioSource.pitch). Damit die Note
    // trotzdem genau ihre MIDI-Länge klingt, braucht ein hoher Ton entsprechend MEHR Sample:
    // Sample-Länge = (klingende Dauer + Release) × Pitch.
    // - Attack: jede Note beginnt mit dem Anfang der Aufnahme
    // - Sustain: reicht die Aufnahme nicht (oder ist die Note lang), wird ein Stück aus der Mitte
    //   mit Überblendung wiederholt -> alle Noten behalten dieselbe Klangfarbe, nichts reißt ab
    // - Release: am Ende weich ausklingen statt abrupt abzuschneiden
    AudioClip GetNoteClip(int noteNumber, double duration, float pitch)
    {
        double sounding = duration * noteLength;
        int count = Mathf.Max(1, (int)((sounding + releaseTime) * pitch * sampleFrequency));

        long key = ((long)noteNumber << 32) | (uint)count;
        if (noteClips.TryGetValue(key, out AudioClip cached)) return cached;

        float[] data = BuildSustainedSample(count);

        // Einblenden (5 ms) gegen Klicks
        int fadeIn = Mathf.Min(count / 4, sampleFrequency / 200);
        for (int i = 0; i < fadeIn; i++) data[i] *= i / (float)fadeIn;

        // Release: über releaseTime (in Ausgabezeit, also × Pitch in Sample-Zeit) ausklingen
        int release = Mathf.Clamp((int)(releaseTime * pitch * sampleFrequency), sampleFrequency / 100, count);
        for (int i = 0; i < release; i++)
        {
            float t = i / (float)release;       // 0 -> 1 über die Ausklingzeit
            data[count - release + i] *= (1f - t) * (1f - t); // weicher als linear
        }

        AudioClip noteClip = AudioClip.Create("Note " + noteNumber + " " + sounding.ToString("0.00") + "s", count, 1, sampleFrequency, false);
        noteClip.SetData(data, 0);
        noteClips[key] = noteClip;
        return noteClip;
    }

    // Liefert 'count' Samples: Anfang der Aufnahme, danach (falls nötig) die Sustain-Schleife
    float[] BuildSustainedSample(int count)
    {
        float[] result = new float[count];
        int total = sampleData.Length;

        int loopStart = Mathf.Clamp((int)(sustainLoopStart * sampleFrequency), 0, total - 1);
        int loopEnd = Mathf.Clamp(loopStart + (int)(sustainLoopLength * sampleFrequency), loopStart + 1, total);
        int loopLen = loopEnd - loopStart;

        // Bis zum Schleifenende einfach die Aufnahme kopieren
        int written = Mathf.Min(count, loopEnd);
        System.Array.Copy(sampleData, result, written);

        // Aufnahme zu kurz für eine sinnvolle Schleife -> Rest bleibt still
        if (loopLen < 8) return result;
        int xf = Mathf.Clamp((int)(sustainCrossfade * sampleFrequency), 1, loopLen / 2);

        // Danach das Schleifenstück immer wieder anhängen; die ersten xf Samples überlappen
        // mit dem Ende des bisher Geschriebenen (Equal-Power-Überblendung)
        while (written < count)
        {
            int pos = written - xf; // Überblendung beginnt xf Samples vor dem bisherigen Ende
            for (int k = 0; k < loopLen && pos + k < count; k++)
            {
                float s = sampleData[loopStart + k];
                int idx = pos + k;
                if (k < xf)
                {
                    float t = k / (float)xf;
                    result[idx] = result[idx] * Mathf.Cos(t * Mathf.PI * 0.5f) + s * Mathf.Sin(t * Mathf.PI * 0.5f);
                }
                else
                {
                    result[idx] = s;
                }
            }
            written = Mathf.Min(count, pos + loopLen);
        }

        return result;
    }

    // Basslinie: pro Takt Grundton und Oktave abwechselnd in Achteln (typischer 8-Bit-Bass)
    void AddBassLine(TempoMap tempoMap, double speed)
    {
        var division = midiFile.TimeDivision as TicksPerQuarterNoteTimeDivision;
        if (division == null) return;

        double quarter = TimeConverter.ConvertTo<MetricTimeSpan>(division.TicksPerQuarterNote, tempoMap).TotalSeconds / speed;
        double eighth = quarter / 2;
        double bar = quarter * 4;
        int bars = Mathf.Min(bassRoots.Length, (int)(loopDuration / bar + 0.01));

        for (int b = 0; b < bars; b++)
        {
            for (int k = 0; k < 8; k++)
            {
                int note = bassRoots[b] + (k % 2 == 1 ? 12 : 0);
                notes.Add(new ScheduledNote
                {
                    time = b * bar + k * eighth,
                    pitch = 1f,
                    clip = GetSynthClip(note, eighth * 0.9, true),
                    volume = bassVolume
                });
            }
        }
    }

    // Einfacher Chiptune-Synth: Rechteckwelle (25 % Pulsbreite) für die Melodie, Dreieckwelle für den Bass.
    // Erzeugt den Ton direkt in der richtigen Frequenz, daher wird er mit Pitch 1 abgespielt.
    AudioClip GetSynthClip(int midiNote, double duration, bool bass)
    {
        int rate = AudioSettings.outputSampleRate;
        const double release = 0.04;
        int count = Mathf.Max(1, (int)((duration + release) * rate));

        string key = (bass ? "B" : "L") + midiNote + "_" + count;
        if (synthClips.TryGetValue(key, out AudioClip cached)) return cached;

        double freq = 440.0 * System.Math.Pow(2, (midiNote - 69) / 12.0);
        double phaseStep = freq / rate;
        double phase = 0;
        int attack = rate / 500;                 // 2 ms
        int holdEnd = (int)(duration * rate);
        int releaseSamples = Mathf.Max(1, count - holdEnd);

        float[] data = new float[count];
        for (int i = 0; i < count; i++)
        {
            float wave = bass
                ? (float)(4.0 * System.Math.Abs(phase - 0.5) - 1.0)  // Dreieck
                : (phase < 0.25 ? 1f : -1f) * 0.6f;                  // Rechteck, leiser (obertonreich)

            float env;
            if (i < attack) env = i / (float)attack;
            else if (i < holdEnd) env = bass ? 1f : 1f - 0.35f * (i / (float)holdEnd); // Lead leicht abfallend
            else env = (bass ? 1f : 0.65f) * (1f - (i - holdEnd) / (float)releaseSamples);

            data[i] = wave * env * 0.5f;

            phase += phaseStep;
            if (phase >= 1.0) phase -= 1.0;
        }

        AudioClip clip = AudioClip.Create("Synth " + key, count, 1, rate, false);
        clip.SetData(data, 0);
        synthClips[key] = clip;
        return clip;
    }

    float GetPitchFromMidiNumber(int midiNumber)
    {
        return Mathf.Pow(2f, (midiNumber - 69f) / 12f);
    }
}
