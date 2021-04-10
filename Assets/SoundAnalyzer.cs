using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class SoundAnalyzer : MonoBehaviour
{
    private const int spectrumSize = 8192;
    private float[] spectrum;

    public AudioMixer mainMixer;

    private const int spectrumTextureWidth = 512;
    private const int spectrumTextureHeight = 200;
    private Texture2D spectrumTexture2D;

    private Note selectedNote;
    private List<Note> notes = new List<Note>();

    private Vector2 screenResolution;
    private bool ignoreSliderEvent = false;

    private int timeWhenLastNoteTriggeredInMS;

    public RawImage spectrumRawImage;

    public Text selectedNoteText;

    public Dropdown noteSelectorDropdown;

    public Slider upperBoundSlider;
    public Slider lowerBoundSlider;

    public Button saveButton;
    public Button loadButton;
    public Button addButton;
    public Button clearButton;

    public Button removeButton;

    public Button nextButton;
    public Button unselectButton;
    public Button previousButton;

    public InputField noteNameInput;

    public GameObject thresholdSliderPanel;
    public GameObject thresholdSliderPrefab;

    public Slider retriggerTimeoutSlider;
    public Slider retriggerLevelSlider;

    public Text retriggerTimeoutText;
    public Text retriggerLevelText;

    public InputField outputInputField;

    private void Awake()
    {
        // We initially save the screen resolution to be later able to reacto to resize events
        screenResolution = new Vector2(Screen.width, Screen.height);
    }

    void Start()
    {
        // Basic setup to get the audio from the mic as spectrum
        spectrum = new float[spectrumSize];
        int samplerate = AudioSettings.outputSampleRate;

        GetComponent<AudioSource>().clip = Microphone.Start(null, true, 10, samplerate);
        GetComponent<AudioSource>().loop = true; 

        // The next line was in the example code, but everything seems to work fine with her excluded
        // while (!(Microphone.GetPosition(null) > 0)){} 

        GetComponent<AudioSource>().Play();
        mainMixer.SetFloat("Main", -80f);

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
        noteSelectorDropdown.onValueChanged.AddListener(delegate 
        { DropdownValueChanged(noteSelectorDropdown); });

        upperBoundSlider.onValueChanged.AddListener(delegate 
        { UpperBoundSliderValueChanged(upperBoundSlider); });

        lowerBoundSlider.onValueChanged.AddListener(delegate 
        { LowerBoundSliderValueChanged(lowerBoundSlider); });

        saveButton.onClick.AddListener(delegate
        { SaveButtonClick(saveButton); });

        loadButton.onClick.AddListener(delegate
        { LoadButtonClick(loadButton); });

        clearButton.onClick.AddListener(delegate
        { ClearButtonClick(clearButton); });

        addButton.onClick.AddListener(delegate
        { AddButtonClick(addButton); });

        nextButton.onClick.AddListener(delegate
        { NextButtonClick(nextButton); });

        previousButton.onClick.AddListener(delegate
        { PreviousButtonClick(previousButton); });

        unselectButton.onClick.AddListener(delegate
        { UnselectButtonClick(unselectButton); });

        retriggerTimeoutSlider.onValueChanged.AddListener(delegate
        { RetriggerTimeoutValueChanged(retriggerTimeoutSlider); });

        retriggerLevelSlider.onValueChanged.AddListener(delegate
        { RetriggerLevelSliderChanged(retriggerLevelSlider); });
    }

    void RetriggerTimeoutValueChanged(Slider retriggerTimeoutSlider)
    {
        // All the notes are updated with the new retrigger timeout value
        foreach (Note note in notes)
        {
            note.minTimeoutFrames = Mathf.RoundToInt(retriggerTimeoutSlider.value);
        }

        // Update the Text label for feedback
        retriggerTimeoutText.text = "Timeout (" + Mathf.RoundToInt(retriggerTimeoutSlider.value) + ")";
    }

    void RetriggerLevelSliderChanged(Slider retriggerLevelSlider)
    {
        // All the notes are updated with the new retrigger level value
        foreach (Note note in notes)
        {
            note.minLevelForRetrigger = retriggerLevelSlider.value;
        }

        // Update the Text label for feedback
        retriggerLevelText.text = "Level (" + (Mathf.Round(retriggerLevelSlider.value * 100) / 100f) + ")";
    }

    void SaveButtonClick(Button saveButton)
    {
        SaveData();
    }

    void LoadButtonClick(Button loadButton)
    {
        LoadData();
        UpdateDropdownOptions();
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
        noteSelectorDropdown.value = notes.FindIndex(x => x == note);

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

    void ClearButtonClick(Button clearButton)
    {
        // Remove all notes from list
        notes.Clear();

        // Destroy all sliders
        foreach(Transform child in thresholdSliderPanel.transform)
        {
            Destroy(child.gameObject);
        }

        // Empty dropdown & unselect
        noteSelectorDropdown.options.Clear();
        UnselectNote();
    }

    void NextButtonClick(Button nextButton)
    {
        Note nextNote;

        if (selectedNote == null)
        {
            // If no note from notes list was selected, first note will be selected
            nextNote = notes[0];
        }
        else if (selectedNote == notes[notes.Count - 1])
        {
            // If last note from notes list was selected, first note will be selected
            nextNote = notes[0];
        }
        else
        {
            // Get current note index and select next
            nextNote = notes[notes.FindIndex(x => x == selectedNote) + 1];
        }

        // Update UI
        SelectNote(nextNote);
    }

    void PreviousButtonClick(Button previousButton)
    {
        Note previousNote;

        if (selectedNote == null)
        {
            // If no note from notes list was selected, last note will be selected
            previousNote = notes[notes.Count - 1];
        }
        else if (selectedNote == notes[0])
        {
            // If first note from notes list was selected, last note will be selected
            previousNote = notes[notes.Count - 1];
        }
        else
        {
            // Get current note index and select previous
            previousNote = notes[notes.FindIndex(x => x == selectedNote) - 1];
        }

        // Update UI
        SelectNote(previousNote);
    }

    void UnselectButtonClick(Button unselectButton)
    {
        UnselectNote();
    }

    void AddButtonClick(Button addButton)
    {
        // Create new note
        Note newNote = new Note(noteNameInput.text, 0, 0, 0);

        // Initialize new note
        newNote.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this);
        newNote.minTimeoutFrames = Mathf.RoundToInt(retriggerTimeoutSlider.value);
        newNote.minLevelForRetrigger = retriggerLevelSlider.value;

        // Add note to notes list, select new note and update UI
        notes.Add(newNote);
        UpdateDropdownOptions();
        SelectNote(newNote);
    }

    void RemoveButtonClick(Button removeButton)
    {
        // Remove note to notes list, unselect note and update UI
        notes.Remove(selectedNote);
        UpdateDropdownOptions();
        UnselectNote();
    }

    void UpdateDropdownOptions()
    {
        // Clear all elements from dropdown list
        noteSelectorDropdown.options.Clear();

        // Create a new Dropdown entry for each note
        foreach (Note note in notes)
        {
            noteSelectorDropdown.options.Add(new Dropdown.OptionData(note.caption));
        }

        // Select the currently selected note (if any)
        if (selectedNote != null)
            noteSelectorDropdown.value = notes.FindIndex(x => x == selectedNote);
    }

    void DropdownValueChanged(Dropdown change)
    {
        // Options count means no options at all, noting to select
        if (change.options.Count == 0)
        {
            return;
        }

        // Otherwise, select note from notes list
        SelectNote(notes[change.value]);
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
            foreach (Note note in notes)
            {
                note.SetThresholdSliderParentPosition();
            }

            // And remember the resolution for next frame
            screenResolution.x = Screen.width;
            screenResolution.y = Screen.height;
        }

        // Then we move the entire spectrum texture by 1 pixel upwards
        for (int x = 0; x < spectrumTextureWidth; x++)
        {
            for (int y = spectrumTextureHeight; y > 0; y--)
            {
                spectrumTexture2D.SetPixel(x, y + 1, spectrumTexture2D.GetPixel(x, y));
            }
        }

        // Now we get the most updated audio spectrum data
        GetComponent<AudioSource>().GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        // For each note, we calculate the level of sound
        foreach(Note note in notes)
        {
            float accumulator = 0;

            // This is done by adding up all the spectum data from the notes lower to upper bound
            for (int i = note.GetLowerBound(); i < note.GetUpperBound(); i++)
            {
                accumulator += spectrum[i];
            }

            // And we pass the level to the note, which then decides if it got triggered or not
            note.SetValue(accumulator);
        }

        // Then we loop over the entire spectrum
        for (int i = 0; i < spectrumSize; i++)
        {
            // But we only consider the lower part, which fits in our texture
            // TODO: this should be done more genericly, and not be bound to the texture size
            if (i < spectrumTextureWidth)
            {
                // First we colorize the current pixel with the color of the current spectrum value
                spectrumTexture2D.SetPixel(i, 0, new Color(MapRange(spectrum[i], 0, 0.01f, 0, 1), 0, 0));

                // Then we iterate over every note
                // TODO: this should be done outside the spectrum loop because 0(n²)
                foreach (Note note in notes)
                {
                    // If the current pixel is a bound, we colorize the pixel differently
                    if (i == note.GetLowerBound() || i == note.GetUpperBound())
                    {
                        if (note.triggered)
                        {
                            // Yellow for triggered
                            spectrumTexture2D.SetPixel(i, 0, Color.yellow);
                        }
                        else if (note == selectedNote)
                        {
                            // Green for selected
                            spectrumTexture2D.SetPixel(i, 0, Color.green);
                        }
                        else
                        {
                            if (Time.frameCount % 2 == 0)
                            {
                                // And dotted blue for reference lines
                                spectrumTexture2D.SetPixel(i, 0, Color.blue);
                            }
                        }
                    }
                }
            }
        }

        // Finally we apply and update the texture
        spectrumTexture2D.Apply();
        spectrumRawImage.texture = spectrumTexture2D;
    }

    public void SaveData()
    {
        // To convert the list of objects to a JSON string, we need to encapsulate her first in another class since the root element cant be a list
        // TODO: Add saving to file
        RootObject rootObject = new RootObject();
        rootObject.notes = notes;

        // Currently, the output is logged where it can be recovered to be put in the kalimbaSetup string
        Debug.Log(JsonUtility.ToJson(rootObject));
    }

    public void LoadData()
    {
        // Currently the data is loaded from this string
        // TODO: Add loading from file
        String kalimbaSetup = "{\"notes\":[{\"caption\":\"C\",\"lowerBound\":85,\"upperBound\":93,\"thresholdValue\":0.025245806202292444},{\"caption\":\"D\",\"lowerBound\":95,\"upperBound\":105,\"thresholdValue\":0.024791114032268525},{\"caption\":\"E\",\"lowerBound\":107,\"upperBound\":117,\"thresholdValue\":0.03157658874988556},{\"caption\":\"F\",\"lowerBound\":118,\"upperBound\":124,\"thresholdValue\":0.026818973943591119},{\"caption\":\"G\",\"lowerBound\":128,\"upperBound\":139,\"thresholdValue\":0.03247590363025665},{\"caption\":\"A\",\"lowerBound\":146,\"upperBound\":156,\"thresholdValue\":0.05438023433089256},{\"caption\":\"B\",\"lowerBound\":164,\"upperBound\":174,\"thresholdValue\":0.0694345161318779},{\"caption\":\"C*\",\"lowerBound\":176,\"upperBound\":185,\"thresholdValue\":0.06816878169775009},{\"caption\":\"D*\",\"lowerBound\":196,\"upperBound\":206,\"thresholdValue\":0.07993388175964356},{\"caption\":\"E*\",\"lowerBound\":222,\"upperBound\":231,\"thresholdValue\":0.155356302857399},{\"caption\":\"F*\",\"lowerBound\":233,\"upperBound\":243,\"thresholdValue\":0.05957638472318649},{\"caption\":\"G*\",\"lowerBound\":264,\"upperBound\":275,\"thresholdValue\":0.10083159804344177},{\"caption\":\"A*\",\"lowerBound\":295,\"upperBound\":309,\"thresholdValue\":0.013621526770293713},{\"caption\":\"B*\",\"lowerBound\":331,\"upperBound\":342,\"thresholdValue\":0.009923440404236317},{\"caption\":\"C**\",\"lowerBound\":353,\"upperBound\":365,\"thresholdValue\":0.015800992026925088},{\"caption\":\"D**\",\"lowerBound\":392,\"upperBound\":404,\"thresholdValue\":0.02569706365466118},{\"caption\":\"E**\",\"lowerBound\":451,\"upperBound\":469,\"thresholdValue\":0.039687380194664}]}";

        // And a root object is recovered from it which then contains the list of notes
        RootObject rootObject = JsonUtility.FromJson<RootObject>(kalimbaSetup);
        notes = rootObject.notes;

        // Only the custom values are recovered. Therefore, we need to reinitialize the notes
        foreach (Note note in notes)
        {
            note.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this);
        }

        // And we need to reset the timeout and level values to the notes
        // TODO: these values should be stored with the notes
        RetriggerTimeoutValueChanged(retriggerTimeoutSlider);
        RetriggerLevelSliderChanged(retriggerLevelSlider);
    }

    public static float MapRange(float value, float inputRangeFrom, float inputRangeTo, float outputRangeFrom, float outputRangeTo)
    {
        // This line maps a value from one range to another
        return (value - inputRangeFrom) * (outputRangeTo - outputRangeFrom) / (inputRangeTo - inputRangeFrom) + outputRangeFrom;
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