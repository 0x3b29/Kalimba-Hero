using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;
using SFB;
using System.IO;
using TMPro;
using System.Linq;

public class SoundAnalyzer : MonoBehaviour
{
    [SerializeField] UiHandler uiHandler;

    [SerializeField] float lowerPeakDetectionThreshold;
    [SerializeField] float upperPeakDetectionThreshold;
    [SerializeField] bool drawRawSpectrum;
    [SerializeField] FilterMode filterMode;

    [SerializeField] int targetFrameRate;
    int oldTargetFrameRate;

    [SerializeField] AudioHandler audioHandler;
    [SerializeField] MidiHandler midiHandler;

    [SerializeField] RawImage spectrumRawImage;
    

    [SerializeField] Slider upperBoundSlider;
    [SerializeField] Slider lowerBoundSlider;

    [SerializeField] Button saveButton;
    [SerializeField] Button loadButton;
    [SerializeField] Button addButton;
    [SerializeField] Button clearButton;
    [SerializeField] Button updateButton;

    [SerializeField] Button removeButton;

    [SerializeField] TMP_InputField noteNameInput;
    [SerializeField] TMP_InputField noteMidiInput;

    [SerializeField] GameObject thresholdSliderPanel;
    [SerializeField] GameObject thresholdSliderPrefab;

    [SerializeField] Slider retriggerLevelSlider;
    [SerializeField] TMP_Text retriggerLevelText;

    [SerializeField] TMP_InputField outputInputField;
    [SerializeField] Button clearOutputButton;

    [SerializeField] Button closeButton;

    public Action<List<string>> audioDevicesListUpdated;
    public Action<Datasource> datasourceUpdated;
    public Action<List<Note>> notesUpdated;
    public Action<Note> noteSelected;
    public Action<Note> noteUpdated;

    const int spectrumTextureWidth = 512;
    const int spectrumTextureHeight = 400;
    Texture2D spectrumTexture2D;
    Color[] spectrumTexturePixels;

    Note selectedNote;
    Datasource datasource;

    Vector2 screenResolution;
    bool ignoreSliderEvent = false;

    int timeWhenLastNoteTriggeredInMS;

    void Awake()
    {
        // We initially save the screen resolution to be later able to reacto to resize events
        screenResolution = new Vector2(Screen.width, Screen.height);

        uiHandler.selectedNoteChanged += OnSelectedNoteChanged;

        uiHandler.selectNextNote += OnSelectNextNote;
        uiHandler.selectPreviousNote += OnSelectPreviousNote;
        uiHandler.unselectNote += OnUnselectNote;
    }

    void Start()
    {
        Application.targetFrameRate = targetFrameRate;
        oldTargetFrameRate = targetFrameRate;

        datasource = new Datasource(1.2f);

        List<string> audioDevices = new List<string>();

        foreach (String device in Microphone.devices)
        {
            audioDevices.Add(device);
        }

        if (audioDevices.Count == 0)
        {
            Debug.LogError("No audio device found.");
            return;
        }

        audioDevicesListUpdated.Invoke(audioDevices);

        datasource.selectedAudioDevice = Microphone.devices[0];

        // Create a new texture which we will use to draw the spectrum into
        spectrumTexture2D = new Texture2D(spectrumTextureWidth, spectrumTextureHeight);
        spectrumTexture2D.filterMode = filterMode;

        // Initialize the new texture with black pixels
        for (int x = 0; x < spectrumTextureWidth; x++)
        {
            for (int y = 0; y < spectrumTextureHeight; y++)
            {
                spectrumTexture2D.SetPixel(x, y, Color.black);
            }
        }

        // Apply and set texture to image component 
        spectrumTexture2D.Apply();
        spectrumRawImage.texture = spectrumTexture2D;
        spectrumTexturePixels = spectrumTexture2D.GetPixels();

        // Create all the delegate functions for the UI compnents
        upperBoundSlider.onValueChanged.AddListener(delegate
        { UpperBoundSliderValueChanged(upperBoundSlider); });

        lowerBoundSlider.onValueChanged.AddListener(delegate
        { LowerBoundSliderValueChanged(lowerBoundSlider); });

        saveButton.onClick.AddListener(delegate
        { SaveButtonClick(); });

        loadButton.onClick.AddListener(delegate
        { LoadButtonClick(); });

        clearButton.onClick.AddListener(delegate
        { ClearButtonClick(); });

        addButton.onClick.AddListener(delegate
        { AddButtonClick(); });

        removeButton.onClick.AddListener(delegate
        { RemoveButtonClick(); });

        updateButton.onClick.AddListener(delegate
        { UpdateButtonClick(); });

        retriggerLevelSlider.onValueChanged.AddListener(delegate
        { RetriggerLevelSliderChanged(retriggerLevelSlider); });

        clearOutputButton.onClick.AddListener(delegate
        {
            outputInputField.text = "";
        });

        closeButton.onClick.AddListener(delegate
        { Application.Quit(); });
    }

    void RetriggerLevelSliderChanged(Slider retriggerLevelSlider)
    {
        // All the notes are updated with the new retrigger level value
        foreach (Note note in datasource.notes)
        {
            note.minRetriggerLevel = retriggerLevelSlider.value;
        }

        // Also remember value in datasource for saveing and loading
        datasource.retriggerMinimumLevel = retriggerLevelSlider.value;

        // Update the Text label for feedback
        retriggerLevelText.text = "Level for retrigger: " + (Mathf.Round(retriggerLevelSlider.value * 100) / 100f);
    }

    void SaveButtonClick()
    {
        // Currently, the output is logged where it can be recovered to be put in the kalimbaSetup string
        Debug.Log(JsonUtility.ToJson(datasource));

        string saveFilePath = StandaloneFileBrowser.SaveFilePanel("Save File", Application.persistentDataPath, "Kalimba-Hero", "kal");

        if (saveFilePath != "")
        {
            StreamWriter writer = new StreamWriter(saveFilePath, false);
            writer.WriteLine(JsonUtility.ToJson(datasource));
            writer.Close();
        }
    }

    void LoadButtonClick()
    {
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Open File", "", new[] { new ExtensionFilter("Kalimba Hero", "kal") }, true);

        if (paths.Length > 0 && File.Exists(paths[0]))
        {
            StreamReader reader = new StreamReader(paths[0]);
            string kalimbaSetup = reader.ReadToEnd();
            reader.Close();

            // And a root object is recovered from it which then contains the list of notes
            datasource = JsonUtility.FromJson<Datasource>(kalimbaSetup);

            // Only the custom values are recovered. Therefore, we need to reinitialize the notes
            foreach (Note note in datasource.notes)
            {
                note.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this, datasource.retriggerMinimumLevel, midiHandler);
            }

            notesUpdated?.Invoke(datasource.notes);

            retriggerLevelSlider.value = datasource.retriggerMinimumLevel;
        }
    }

    void UnselectNote()
    {
        selectedNote = null;
        noteSelected?.Invoke(null);

        // If no note is selected, there is no need for the bounds sliders to be visible
        lowerBoundSlider.gameObject.SetActive(false);
        upperBoundSlider.gameObject.SetActive(false);
    }

    void OnSelectedNoteChanged(string selectedNoteName)
    {
        Note selectedNote = datasource.notes.Find(x => x.caption == selectedNoteName);
        SelectNote(selectedNote);
    }

    void SelectNote(Note note)
    {
        selectedNote = note;
        
        noteSelected?.Invoke(note);
        noteNameInput.text = note.caption;
        noteMidiInput.text = note.midiValue.ToString();


        // While initially setting the bounds, we need to prevent the slider update event to be triggered 
        // E.g. after setting the lowerBoundSlider.value, the event already fires and uses an old upperBoundSlider.value
        ignoreSliderEvent = true;
        lowerBoundSlider.value = note.GetLowerBound();
        upperBoundSlider.value = note.GetUpperBound();
        ignoreSliderEvent = false;

        // Make sure the sliders are visible again (Only important if no note was previously selected)
        lowerBoundSlider.gameObject.SetActive(true);
        upperBoundSlider.gameObject.SetActive(true);
    }

    void ClearButtonClick()
    {
        // Remove all notes from list
        datasource.notes.Clear();

        // Destroy all sliders
        foreach (Transform child in thresholdSliderPanel.transform)
        {
            Destroy(child.gameObject);
        }

        // Empty dropdown & unselect

        notesUpdated?.Invoke(datasource.notes);
        UnselectNote();
    }

    void OnSelectNextNote()
    {
        Note nextNote;

        if (selectedNote == null)
        {
            // If no note from notes list was selected, first note will be selected
            nextNote = datasource.notes[0];
        }
        else if (selectedNote == datasource.notes[datasource.notes.Count - 1])
        {
            // If last note from notes list was selected, first note will be selected
            nextNote = datasource.notes[0];
        }
        else
        {
            // Get current note index and select next
            nextNote = datasource.notes[datasource.notes.FindIndex(x => x == selectedNote) + 1];
        }

        // Update UI
        SelectNote(nextNote);
    }

    void OnSelectPreviousNote()
    {
        Note previousNote;

        if (selectedNote == null)
        {
            // If no note from notes list was selected, last note will be selected
            previousNote = datasource.notes[datasource.notes.Count - 1];
        }
        else if (selectedNote == datasource.notes[0])
        {
            // If first note from notes list was selected, last note will be selected
            previousNote = datasource.notes[datasource.notes.Count - 1];
        }
        else
        {
            // Get current note index and select previous
            previousNote = datasource.notes[datasource.notes.FindIndex(x => x == selectedNote) - 1];
        }

        // Update UI
        SelectNote(previousNote);
    }

    void OnUnselectNote()
    {
        UnselectNote();
    }

    void AddButtonClick()
    {
        byte midiValue = 0;
        byte.TryParse(noteMidiInput.text, out midiValue);
        midiValue = (byte)Mathf.Min(127, (int)midiValue);

        // Create new note
        Note newNote = new Note(noteNameInput.text, midiValue, 0, 0, 0);

        // Initialize new note
        newNote.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this, retriggerLevelSlider.value, midiHandler);

        // Add note to notes list, select new note and update UI
        datasource.notes.Add(newNote);
        notesUpdated?.Invoke(datasource.notes);
        SelectNote(newNote);
    }

    void UpdateButtonClick()
    {
        if (selectedNote != null)
        {
            byte midiValue = 0;
            byte.TryParse(noteMidiInput.text, out midiValue);
            midiValue = (byte)Mathf.Min(127, (int)midiValue);

            selectedNote.caption = noteNameInput.text;
            selectedNote.midiValue = midiValue;

            noteUpdated?.Invoke(selectedNote);
        }
    }

    void RemoveButtonClick()
    {
        // Remove note to notes list, unselect note and update UI
        datasource.notes.Remove(selectedNote);
        notesUpdated?.Invoke(datasource.notes);

        UnselectNote();
    }

    void UpperBoundSliderValueChanged(Slider upperBoundSlider)
    {
        if (selectedNote == null || ignoreSliderEvent == true)
            return;

        // We want to make sure that the upperBoundSlider.value is at least lowerBoundSlider.value
        if (upperBoundSlider.value < lowerBoundSlider.value)
            lowerBoundSlider.value = upperBoundSlider.value;

        // Set bounds to note will update threshold position 
        selectedNote.SetNewBounds(Mathf.RoundToInt(lowerBoundSlider.value), Mathf.RoundToInt(upperBoundSlider.value));
    }

    void LowerBoundSliderValueChanged(Slider lowerBoundSlider)
    {
        if (selectedNote == null || ignoreSliderEvent == true)
            return;

        // We want to make sure that the lowerBoundSlider.value is at most upperBoundSlider.value
        if (lowerBoundSlider.value > upperBoundSlider.value)
            upperBoundSlider.value = lowerBoundSlider.value;

        // Set bounds to note will update threshold position 
        selectedNote.SetNewBounds(Mathf.RoundToInt(lowerBoundSlider.value), Mathf.RoundToInt(upperBoundSlider.value));
    }

    // Define a timer and the interval for 24 updates per second
    private float updateInterval = 1.0f / 24.0f;
    private float timer = 0f;

    void Update()
    {
        // First we check if the screen size changed
        if (screenResolution.x != Screen.width || screenResolution.y != Screen.height)
        {
            // If so, we need to reposition all the threshold slider
            foreach (Note note in datasource.notes)
            {
                note.SetThresholdSliderParentPosition();
            }

            // And remember the resolution for next frame
            screenResolution.x = Screen.width;
            screenResolution.y = Screen.height;
        }

        if (oldTargetFrameRate != targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
            oldTargetFrameRate = targetFrameRate;
        }

        processAudio();

        // Accumulate time passed since last frame
        timer += Time.deltaTime;

        // Check if the accumulated time exceeds or equals the update interval
        if (timer >= updateInterval)
        {
            // Update the texture
            spectrumTexture2D.SetPixels(spectrumTexturePixels);
            spectrumTexture2D.Apply();

            // Reset the timer, subtracting the update interval to handle any overflow
            timer -= updateInterval;
        }
    }

    public void ShiftTextureUpByOneLine(Color[] pixels)
    {
        for (int i = pixels.Length - 1; i >= spectrumTextureWidth; i--)
        {
            pixels[i] = pixels[i - spectrumTextureWidth];
        }
    }

    public void ColorLastTextureLine(Color[] pixels, Color color)
    {
        for (int i = 0; i < spectrumTextureWidth; i++)
        {
            pixels[i] = color;
        }
    }

    void processAudio()
    {
        // Then shift the entire texture such that all the pixels are one entire row further down
        // This is done from back to front because otherwse the first row would be written to all rows

        ShiftTextureUpByOneLine(spectrumTexturePixels);
        ColorLastTextureLine(spectrumTexturePixels, Color.black);

        float[] spectrum = audioHandler.GetSpectrumData();

        // Next we loop over the entire spectrum and add a new line of pixels with the most recent audio data
        for (int i = 0; i < spectrum.Length; i++)
        {
            // But we only consider the lower part, which fits in our texture
            // TODO: this should be done more genericly, and not be bound to the texture size
            if (i > spectrumTextureWidth)
            {
                continue;
            }

            if (!drawRawSpectrum)
            {
                continue;
            }

            // First we colorize the current pixel with the color of the current spectrum value
            spectrumTexturePixels[i] = Helpers.MapValueToColor(spectrum[i]);
        }

        List<int> peaks = audioHandler.DetectPeaks(spectrum, spectrumTextureWidth, lowerPeakDetectionThreshold, upperPeakDetectionThreshold);

        // Then we iterate over every note
        foreach (Note note in datasource.notes)
        {
            if (Time.frameCount % 10 == 0 || Time.frameCount % 10 == 1)
            {
                // And dotted blue for reference lines
                spectrumTexturePixels[note.GetLowerBound()] = Color.white;
                spectrumTexturePixels[note.GetUpperBound()] = Color.white;
            }

            if (note == selectedNote)
            {
                // Green for selected
                spectrumTexturePixels[note.GetLowerBound()] = Color.green;
                spectrumTexturePixels[note.GetUpperBound()] = Color.green;
            }

            note.IncFrameCounter();

            // For each note, we calculate the level of sound
            float accumulator = 0;

            bool foundPeak = false;
            for (int i = 0; i < peaks.Count; i++)
            {
                if (peaks[i] > note.lowerBound && peaks[i] < note.upperBound)
                {
                    foundPeak = true;
                }
            }

            // This is done by adding up all the spectum data from the notes lower to upper bound
            for (int i = note.GetLowerBound(); i <= note.GetUpperBound(); i++)
            {
                accumulator += spectrum[i];
            }

            note.SetValue(accumulator, foundPeak);

            // Then mark the spectrum of the note according to its state
            if (note.noteState != NoteState.notTriggered)
            {
                // Yellow for triggered

                if (note.framesSinceTriggered == 0)
                {
                    int xMin = Mathf.Max(note.GetLowerBound() - 5, 0);
                    int xMax = Mathf.Min(note.GetUpperBound() + 5, spectrumTextureWidth);

                    for (int i = xMin; i < xMax; i++)
                    {
                        spectrumTexturePixels[i] = Color.white;
                    }


                }

                Color color = Color.Lerp(Color.yellow, Color.blue, Helpers.MapRange(note.framesSinceTriggered, 0, 10, 0, 1));
                spectrumTexturePixels[note.GetLowerBound()] = color;
                spectrumTexturePixels[note.GetUpperBound()] = color;
            }
        }

        foreach (int peak in peaks)
        {
            spectrumTexturePixels[peak] = Color.white;
        }
    }

    public void UpdateUIForTriggeredNote(string note)
    {
        // This funciton is called from the notes to update the update the UI

        if (outputInputField.text == "")
        {
            // If the output field is empty, we set the triggered note and remember the time
            timeWhenLastNoteTriggeredInMS = Mathf.RoundToInt(Time.time * 1000);
            outputInputField.text = note + ", ";
        }
        else
        {
            // If the output field already contains data, we append the difference between the time when last note triggered and now as well as the new note
            outputInputField.text += Mathf.RoundToInt(Time.time * 1000) - timeWhenLastNoteTriggeredInMS + "; " + note + ", ";
            timeWhenLastNoteTriggeredInMS = Mathf.RoundToInt(Time.time * 1000);
        }
    }
}