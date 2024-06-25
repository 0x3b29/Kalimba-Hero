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
    [SerializeField]
    AudioHandler audioHandler;

    [SerializeField] int targetFrameRate;
    int oldTargetFrameRate;

    [SerializeField] float soundAverage;

    const int spectrumTextureWidth = 512;
    const int spectrumTextureHeight = 400;
    Texture2D spectrumTexture2D;

    Note selectedNote;
    Datasource datasource;

    Vector2 screenResolution;
    bool ignoreSliderEvent = false;

    int timeWhenLastNoteTriggeredInMS;


    [SerializeField] RawImage spectrumRawImage;

    [SerializeField] TMP_Dropdown inputDeviceDropdown;

    [SerializeField] TMP_Text selectedNoteText;

    [SerializeField] TMP_Dropdown noteSelectorDropdown;

    [SerializeField] Slider upperBoundSlider;
    [SerializeField] Slider lowerBoundSlider;

    [SerializeField] Button saveButton;
    [SerializeField] Button loadButton;
    [SerializeField] Button addButton;
    [SerializeField] Button clearButton;
    [SerializeField] Button renameButton;

    [SerializeField] Button removeButton;

    [SerializeField] Button nextButton;
    [SerializeField] Button unselectButton;
    [SerializeField] Button previousButton;


    [SerializeField] TMP_InputField noteNameInput;

    [SerializeField] GameObject thresholdSliderPanel;
    [SerializeField] GameObject thresholdSliderPrefab;


    [SerializeField] Slider retriggerLevelSlider;
    [SerializeField] TMP_Text retriggerLevelText;

    [SerializeField] TMP_InputField outputInputField;
    [SerializeField] Button clearOutputButton;

    [SerializeField] Button closeButton;

    void Awake()
    {
        // We initially save the screen resolution to be later able to reacto to resize events
        screenResolution = new Vector2(Screen.width, Screen.height);
    }

    void Start()
    {
        Application.targetFrameRate = targetFrameRate;
        oldTargetFrameRate = targetFrameRate;

        datasource = new Datasource(1.2f);

        foreach (String device in Microphone.devices)
        {
            inputDeviceDropdown.options.Add(new TMP_Dropdown.OptionData(device));
        }

        if (inputDeviceDropdown.options.Count == 0)
        {
            Debug.LogError("No audio device found.");
            return;
        }

        datasource.selectedAudioDevice = Microphone.devices[0];
        inputDeviceDropdown.value = 0;
        inputDeviceDropdown.RefreshShownValue();

        string audioDeviceName = inputDeviceDropdown.options[0].text;

        audioHandler.UpdateAudioSource(audioDeviceName);

        // Create a new texture which we will use to draw the spectrum into
        spectrumTexture2D = new Texture2D(spectrumTextureWidth, spectrumTextureHeight);

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

        // Create all the delegate functions for the UI compnents
        inputDeviceDropdown.onValueChanged.AddListener(delegate
        { InputDeviceDropdownValueChanged(inputDeviceDropdown); });

        noteSelectorDropdown.onValueChanged.AddListener(delegate
        { noteSelectorDropdownValueChanged(noteSelectorDropdown); });

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

        renameButton.onClick.AddListener(delegate
        { RenameButtonClick(); });

        nextButton.onClick.AddListener(delegate
        { NextButtonClick(); });

        previousButton.onClick.AddListener(delegate
        { PreviousButtonClick(); });

        unselectButton.onClick.AddListener(delegate
        { UnselectButtonClick(); });

        retriggerLevelSlider.onValueChanged.AddListener(delegate
        { RetriggerLevelSliderChanged(retriggerLevelSlider); });

        clearOutputButton.onClick.AddListener(delegate
        {
            outputInputField.text = "";
        });

        closeButton.onClick.AddListener(delegate
        { Application.Quit(); });
    }

    void InputDeviceDropdownValueChanged(TMP_Dropdown inputDeviceDropdown)
    {
        if (inputDeviceDropdown.options[inputDeviceDropdown.value].text != datasource.selectedAudioDevice)
        {
            string deviceName = inputDeviceDropdown.options[inputDeviceDropdown.value].text;

            audioHandler.UpdateAudioSource(deviceName);
            datasource.selectedAudioDevice = deviceName;
        }
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

            // In case previously selected Mic is not available, we select the first one that is
            if (!Microphone.devices.Any(x => x.Equals(datasource.selectedAudioDevice)))
            {
                datasource.selectedAudioDevice = Microphone.devices[0];
            }
            else
            {
                int inputDeviceIndex = 0;

                foreach (TMP_Dropdown.OptionData optionData in inputDeviceDropdown.options)
                {
                    if (optionData.text.Equals(datasource.selectedAudioDevice))
                    {
                        inputDeviceIndex = inputDeviceDropdown.options.IndexOf(optionData);
                    }
                }

                inputDeviceDropdown.value = inputDeviceIndex;
                inputDeviceDropdown.RefreshShownValue();
            }

            audioHandler.UpdateAudioSource(datasource.selectedAudioDevice);

            // Only the custom values are recovered. Therefore, we need to reinitialize the notes
            foreach (Note note in datasource.notes)
            {
                note.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this, datasource.retriggerMinimumLevel);
            }

            UpdateDropdownOptions();

            retriggerLevelSlider.value = datasource.retriggerMinimumLevel;
        }
    }

    void UnselectNote()
    {
        selectedNote = null;
        selectedNoteText.text = "/";

        // If no note is selected, there is no need for the bounds sliders to be visible
        lowerBoundSlider.gameObject.SetActive(false);
        upperBoundSlider.gameObject.SetActive(false);
    }

    void SelectNote(Note note)
    {
        selectedNote = note;
        selectedNoteText.text = note.caption;

        // Try to find the correct entry in the dropdown
        noteSelectorDropdown.value = datasource.notes.FindIndex(x => x == note);

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
        noteSelectorDropdown.options.Clear();
        UnselectNote();
        noteSelectorDropdown.RefreshShownValue();
    }

    void NextButtonClick()
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

    void PreviousButtonClick()
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

    void UnselectButtonClick()
    {
        UnselectNote();
    }

    void AddButtonClick()
    {
        // Create new note
        Note newNote = new Note(noteNameInput.text, 0, 0, 0);

        // Initialize new note
        newNote.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this, retriggerLevelSlider.value);

        // Add note to notes list, select new note and update UI
        datasource.notes.Add(newNote);
        UpdateDropdownOptions();
        SelectNote(newNote);
    }

    void RenameButtonClick()
    {
        if (selectedNote != null)
        {
            selectedNote.caption = noteNameInput.text;
            UpdateDropdownOptions();
            selectedNoteText.text = selectedNote.caption;
        }
    }

    void RemoveButtonClick()
    {
        // Remove note to notes list, unselect note and update UI
        datasource.notes.Remove(selectedNote);
        UpdateDropdownOptions();
        UnselectNote();
    }

    void UpdateDropdownOptions()
    {
        // Clear all elements from dropdown list
        noteSelectorDropdown.options.Clear();

        // Create a new Dropdown entry for each note
        foreach (Note note in datasource.notes)
        {
            noteSelectorDropdown.options.Add(new TMP_Dropdown.OptionData(note.caption));
        }

        // Select the currently selected note (if any)
        if (selectedNote != null)
            noteSelectorDropdown.value = datasource.notes.FindIndex(x => x == selectedNote);

        noteSelectorDropdown.RefreshShownValue();
    }

    void noteSelectorDropdownValueChanged(TMP_Dropdown change)
    {
        // Options count means no options at all, noting to select
        if (change.options.Count == 0)
        {
            return;
        }

        // Otherwise, select note from notes list
        SelectNote(datasource.notes[change.value]);
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
    }

    void processAudio()
    {
        // Then shift the entire texture such that all the pixels are one entire row further down
        // This is done from back to front because otherwse the first row would be written to all rows
        Color[] pixels = spectrumTexture2D.GetPixels();

        for (int i = pixels.Length - 1; i >= spectrumTextureWidth; i--)
        {
            pixels[i] = pixels[i - spectrumTextureWidth];
        }

        float[] spectrum = audioHandler.GetSpectrumData();

        float soundLevel = 0;

        // Next we loop over the entire spectrum and add a new line of pixels with the most recent audio data
        for (int i = 0; i < spectrum.Length; i++)
        {
            soundLevel += spectrum[i];

            // But we only consider the lower part, which fits in our texture
            // TODO: this should be done more genericly, and not be bound to the texture size
            if (i < spectrumTextureWidth)
            {
                // First we colorize the current pixel with the color of the current spectrum value
                pixels[i] = Helpers.MapValueToColor(spectrum[i]);
            }
        }

        soundAverage = soundLevel / spectrum.Length * 100000;

        spectrumTexture2D.SetPixels(pixels);

        // Then we iterate over every note
        foreach (Note note in datasource.notes)
        {
            note.IncFrameCounter();

            // For each note, we calculate the level of sound
            float accumulator = 0;
            float max = -1;
            int maxPosition = -1;

            // This is done by adding up all the spectum data from the notes lower to upper bound
            for (int i = note.GetLowerBound(); i <= note.GetUpperBound(); i++)
            {
                accumulator += spectrum[i];

                // Also we want to know the position of the highest sound level
                if (spectrum[i] > max)
                {
                    max = spectrum[i];
                    maxPosition = i;
                }
            }

            int peakPosition = maxPosition;
            int direction;

            if (spectrum[peakPosition + 1] > spectrum[peakPosition - 1])
            {
                direction = 1;
            }
            else
            {
                direction = -1;
            }

            if (note.thresholdValue > accumulator)
            {
                while (spectrum[peakPosition + direction] > spectrum[peakPosition])
                {
                    peakPosition += direction;
                }
            }

            if (peakPosition < note.GetUpperBound() && peakPosition > note.GetLowerBound())
            {
                note.SetValue(accumulator);
            }

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
                        spectrumTexture2D.SetPixel(i, 0, Color.white);
                    }
                }

                Color color = Color.Lerp(Color.yellow, Color.blue, Helpers.MapRange(note.framesSinceTriggered, 0, 10, 0, 1));
                spectrumTexture2D.SetPixel(note.GetLowerBound(), 0, color);
                spectrumTexture2D.SetPixel(note.GetUpperBound(), 0, color);
            }
            else if (note == selectedNote)
            {
                // Green for selected
                spectrumTexture2D.SetPixel(note.GetLowerBound(), 0, Color.green);
                spectrumTexture2D.SetPixel(note.GetUpperBound(), 0, Color.green);
            }
            else
            {
                if (Time.frameCount % 10 == 0 || Time.frameCount % 10 == 1)
                {
                    // And dotted blue for reference lines
                    spectrumTexture2D.SetPixel(note.GetLowerBound(), 0, Color.white);
                    spectrumTexture2D.SetPixel(note.GetUpperBound(), 0, Color.white);
                }
            }
        }

        // Finally we apply and update the texture
        spectrumTexture2D.Apply();
        spectrumRawImage.texture = spectrumTexture2D;
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