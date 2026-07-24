using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections;
using System.IO;

public class MidiWavPlayer : MonoBehaviour
{
    [Header("Dateinamen (in Assets/StreamingAssets)")]
    public string midiFileName = "";
    public string wavFileName = "";

    [Header("Referenzen")]
    public AudioSource audioSource;

    private MidiFile midiFile;
    private AudioClip sampleClip;

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
        string wavPath = Path.Combine(Application.streamingAssetsPath, wavFileName);
        string cleanWavPath = wavPath.Replace("file://", "");

        byte[] wavBytes = null;

        // Prüfen ob Datei existiert und laden
        if (File.Exists(cleanWavPath))
        {
            wavBytes = File.ReadAllBytes(cleanWavPath);
        }
        else
        {
            Debug.LogError("WAV Datei nicht gefunden: " + cleanWavPath);
            yield break; // WICHTIG: Beendet die Coroutine hier sauber
        }

        if (wavBytes != null)
        {
            // Versuch die Bytes zu laden
            sampleClip = WavUtility.ToAudioClip(wavBytes);

            // Fallback falls die Byte-Methode nicht geht (abhängig von WavUtility Version)
            if (sampleClip == null)
            {
                sampleClip = WavUtility.ToAudioClip("file://" + cleanWavPath);
            }
        }

        if (sampleClip == null)
        {
            Debug.LogError("Fehler: Konnte WAV nicht decodieren. Ist es eine 16-Bit PCM WAV?");
            yield break; // WICHTIG: Beendet die Coroutine hier sauber
        }

        Debug.Log("WAV geladen: " + wavFileName);
        yield return null; // Kurze Pause für den Compiler/Unity Kontext

        // --- 2. MIDI LADEN ---
        string midiPath = Path.Combine(Application.streamingAssetsPath, midiFileName);
        string cleanMidiPath = midiPath.Replace("file://", "");

        byte[] midiBytes = null;

        if (File.Exists(cleanMidiPath))
        {
            midiBytes = File.ReadAllBytes(cleanMidiPath);
        }
        else
        {
            Debug.LogError("MIDI Datei nicht gefunden: " + cleanMidiPath);
            yield break; // WICHTIG: Beendet die Coroutine hier sauber
        }

        if (midiBytes != null)
        {
            try
            {
                using (var stream = new MemoryStream(midiBytes))
                {
                    midiFile = MidiFile.Read(stream);
                }
                Debug.Log("MIDI geladen: " + midiFileName);
            }
            catch (System.Exception e)
            {
                Debug.LogError("Fehler beim Lesen der MIDI: " + e.Message);
                yield break; // WICHTIG: Beendet die Coroutine hier sauber
            }
        }
        else
        {
            Debug.LogError("MIDI Bytes sind null.");
            yield break;
        }

        Debug.Log("Alle Dateien geladen. Starte Wiedergabe...");

        // Startet die Wiedergabe
        PlayMidiWithSample();

        yield return null; // Sicherstellt, dass die Coroutine sauber endet
    }

    void PlayMidiWithSample()
    {
        if (midiFile == null || sampleClip == null) return;

        var notes = midiFile.GetNotes();
        foreach (var note in notes)
        {
            StartCoroutine(PlayNoteAtTime(note, sampleClip));
        }
    }

    IEnumerator PlayNoteAtTime(Note note, AudioClip clip)
    {
        float startTime = (float)note.TimeAs<MetricTimeSpan>(midiFile.GetTempoMap()).TotalSeconds;
        yield return new WaitForSeconds(startTime);

        float pitch = GetPitchFromMidiNumber(note.NoteNumber);
        if (pitch > 0)
        {
            audioSource.pitch = pitch;
            audioSource.PlayOneShot(clip);
        }
    }

    float GetPitchFromMidiNumber(int midiNumber)
    {
        return Mathf.Pow(2f, (midiNumber - 69f) / 12f);
    }
}