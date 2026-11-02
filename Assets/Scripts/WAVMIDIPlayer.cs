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

    private MidiFile midiFile;
    private AudioClip sampleClip;

    // Eine vorberechnete Note: Startzeit relativ zum Durchlaufbeginn + Tonhöhe
    private struct ScheduledNote
    {
        public double time;
        public float pitch;
    }

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
        string cleanWavPath = Path.Combine(Application.streamingAssetsPath, wavFileName).Replace("file://", "");

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

        Debug.Log("WAV geladen: " + wavFileName);
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

        // Alle Noten einmal vorberechnen (TempoMap nur einmal holen) und nach Zeit sortieren
        TempoMap tempoMap = midiFile.GetTempoMap();
        double speed = Mathf.Max(0.01f, midiSpeed);

        notes = new List<ScheduledNote>();
        loopDuration = 0;

        foreach (var note in midiFile.GetNotes())
        {
            double start = note.TimeAs<MetricTimeSpan>(tempoMap).TotalSeconds / speed;
            double end = TimeConverter.ConvertTo<MetricTimeSpan>(note.EndTime, tempoMap).TotalSeconds / speed;

            notes.Add(new ScheduledNote
            {
                time = start,
                pitch = GetPitchFromMidiNumber(note.NoteNumber)
            });

            if (end > loopDuration) loopDuration = end;
        }
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
                // Pitch nur auf DIESER Voice setzen -> andere klingende Noten bleiben unverändert
                src.pitch = n.pitch;
                src.PlayScheduled(startDsp);
                voiceEndDsp[v] = startDsp + sampleClip.length / n.pitch;
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

    float GetPitchFromMidiNumber(int midiNumber)
    {
        return Mathf.Pow(2f, (midiNumber - 69f) / 12f);
    }
}
