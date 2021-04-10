using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class SoundAnalyzer : MonoBehaviour
{
    public int qSamples = 8192;
    public int binSize = 8192;

    float[] spectrum;
    int samplerate;

    public Text display;
    public bool mute;
    public AudioMixer masterMixer;

    int textureWidth = 512;
    int textureHeight = 200;
    Texture2D texture2d;

    Note selectedNote;
    List<Note> notes = new List<Note>();

    public Text selectedNoteText;

    public Dropdown noteSelectorDropdown;

    public Slider upperBoundSlider;
    public Slider lowerBoundSlider;

    public Button saveButton;
    public Button loadButton;
    public Button addButton;
    public Button clearButton;

    public Button removeButton;
    public Button unselectButton;

    public Button nextButton;
    public Button previousButton;

    public InputField noteNameInput;

    public GameObject thresholdSliderPanel;
    public GameObject thresholdSliderPrefab;

    public Slider retriggerTimeoutSlider;
    public Slider retriggerLevelSlider;

    public Text retriggerTimeoutText;
    public Text retriggerLevelText;

    public InputField outputInputField;

    private Vector2 resolution;

    private bool ignoreSliderEvent = false;

    private void Awake()
    {
        resolution = new Vector2(Screen.width, Screen.height);
    }

    void Start()
    {
        spectrum = new float[binSize];
        samplerate = AudioSettings.outputSampleRate;

        // starts the Microphone and attaches it to the AudioSource
        GetComponent<AudioSource>().clip = Microphone.Start(null, true, 10, samplerate);
        GetComponent<AudioSource>().loop = true; // Set the AudioClip to loop
        while (!(Microphone.GetPosition(null) > 0)) { } // Wait until the recording has started
        GetComponent<AudioSource>().Play();

        // Mutes the mixer. You have to expose the Volume element of your mixer for this to work. I named mine "masterVolume".
        masterMixer.SetFloat("masterVolume", -80f);

        texture2d = new Texture2D(textureWidth, textureHeight);

        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = 0; y < textureHeight; y++)
            {
                texture2d.SetPixel(x, y, Color.black);
            }
        }

        texture2d.Apply();

        GameObject.Find("Image").GetComponent<RawImage>().texture = texture2d;

        UpdateDropdownOptions();
        DropdownValueChanged(noteSelectorDropdown);

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

        DropdownValueChanged(noteSelectorDropdown);

        retriggerTimeoutSlider.onValueChanged.AddListener(delegate
        { RetriggerTimeoutValueChanged(retriggerTimeoutSlider); });

        retriggerLevelSlider.onValueChanged.AddListener(delegate
        { RetriggerLevelSliderChanged(retriggerLevelSlider); });
    }

    void RetriggerTimeoutValueChanged(Slider retriggerTimeoutSlider)
    {
        foreach(Note note in notes)
        {
            note.minTimeoutFrames = Mathf.RoundToInt(retriggerTimeoutSlider.value);
        }

        retriggerTimeoutText.text = "Timeout (" + Mathf.RoundToInt(retriggerTimeoutSlider.value) + ")";
    }

    void RetriggerLevelSliderChanged(Slider retriggerLevelSlider)
    {
        foreach (Note note in notes)
        {
            note.minLevelForRetrigger = retriggerLevelSlider.value;
        }

        retriggerLevelText.text = "Level (" + (Mathf.Round(retriggerLevelSlider.value * 100) / 100f) + ")";
    }

    void SaveButtonClick(Button saveButton)
    {
        SaveData();
    }

    void LoadButtonClick(Button loadButton)
    {
        LoadData();
        UnselectNote();
        UpdateDropdownOptions();
    }

    void UnselectNote()
    {
        selectedNote = null;
        selectedNoteText.text = "/";
        lowerBoundSlider.gameObject.SetActive(false);
        upperBoundSlider.gameObject.SetActive(false);
    }

    void SelectNote(Note note)
    {
        noteSelectorDropdown.value = notes.FindIndex(x => x == note);
        selectedNote = note;
        selectedNoteText.text = note.caption;

        ignoreSliderEvent = true;
        lowerBoundSlider.value = note.GetLowerBound();
        upperBoundSlider.value = note.GetUpperBound();
        ignoreSliderEvent = false;

        lowerBoundSlider.gameObject.SetActive(true);
        upperBoundSlider.gameObject.SetActive(true);
    }

    void ClearButtonClick(Button clearButton)
    {
        notes.Clear();

        foreach(Transform child in thresholdSliderPanel.transform)
        {
            Destroy(child.gameObject);
        }

        noteSelectorDropdown.options.Clear();
        UnselectNote();
    }

    void NextButtonClick(Button nextButton)
    {
        Note nextNote;

        if (selectedNote == null)
        {
            nextNote = notes[0];
        }
        else if (selectedNote == notes[notes.Count - 1])
        {
            nextNote = notes[0];
        }
        else
        {
            nextNote = notes[notes.FindIndex(x => x == selectedNote) + 1];
        }

        SelectNote(nextNote);
    }

    void PreviousButtonClick(Button previousButton)
    {
        Note previousNote;

        if (selectedNote == null)
        {
            previousNote = notes[notes.Count - 1];
        }
        else if (selectedNote == notes[0])
        {
            previousNote = notes[notes.Count - 1];
        }
        else
        {
            previousNote = notes[notes.FindIndex(x => x == selectedNote) - 1];
        }

        SelectNote(previousNote);
    }

    void UnselectButtonClick(Button unselectButton)
    {
        selectedNote = null;
        selectedNoteText.text = "/";
    }

    void AddButtonClick(Button addButton)
    {
        Note newNote = new Note(noteNameInput.text, 0, 0, 0);

        newNote.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this);
        newNote.minTimeoutFrames = Mathf.RoundToInt(retriggerTimeoutSlider.value);
        newNote.minLevelForRetrigger = retriggerLevelSlider.value;

        notes.Add(newNote);
        UpdateDropdownOptions();
        SelectNote(newNote);
    }

    void RemoveButtonClick(Button removeButton)
    {
        notes.Remove(selectedNote);
        UpdateDropdownOptions();
        UnselectNote();
    }

    void UpdateDropdownOptions()
    {
        noteSelectorDropdown.options.Clear();

        foreach (Note note in notes)
        {
            noteSelectorDropdown.options.Add(new Dropdown.OptionData(note.caption));
        }
    }

    void DropdownValueChanged(Dropdown change)
    {
        if (change.options.Count == 0)
        {
            return;
        }

        SelectNote(notes[change.value]);
    }

    void UpperBoundSliderValueChanged(Slider upperBoundSlider)
    {
        if (selectedNote == null || ignoreSliderEvent == true)
            return;

        if (upperBoundSlider.value < lowerBoundSlider.value)
            lowerBoundSlider.value = upperBoundSlider.value;

        selectedNote.SetNewBounds(Mathf.RoundToInt(lowerBoundSlider.value), Mathf.RoundToInt(upperBoundSlider.value));
    }

    void LowerBoundSliderValueChanged(Slider lowerBoundSlider)
    {
        if (selectedNote == null || ignoreSliderEvent == true)
            return;

        if (lowerBoundSlider.value > upperBoundSlider.value)
            upperBoundSlider.value = lowerBoundSlider.value;

        selectedNote.SetNewBounds(Mathf.RoundToInt(lowerBoundSlider.value), Mathf.RoundToInt(upperBoundSlider.value));
    }

    void Update()
    {
        if (resolution.x != Screen.width || resolution.y != Screen.height)
        {
            foreach (Note note in notes)
            {
                note.SetThresholdSliderParentPosition(thresholdSliderPanel.GetComponent<RectTransform>().rect.width);
            }

            resolution.x = Screen.width;
            resolution.y = Screen.height;
        }

        for (int x = 0; x < textureWidth; x++)
        {
            for (int y = textureHeight; y > 0; y--)
            {
                texture2d.SetPixel(x, y + 1, texture2d.GetPixel(x, y));
            }
        }

        GetComponent<AudioSource>().GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        foreach(Note note in notes)
        {
            float accumulator = 0;

            for (int i = note.GetLowerBound(); i < note.GetUpperBound(); i++)
            {
                accumulator += spectrum[i];
            }

            note.SetValue(accumulator);
        }

        for (int i = 0; i < binSize; i++)
        {
            if (i < textureWidth)
            {
                texture2d.SetPixel(i, 0, new Color(MapRange(spectrum[i], 0, 0.01f, 0, 1), 0, 0));

                foreach (Note note in notes)
                {
                    if (i == note.GetLowerBound() || i == note.GetUpperBound())
                    {

                        if (note.triggered)
                        {
                            texture2d.SetPixel(i, 0, Color.yellow);
                        }
                        else if (note == selectedNote)
                        {
                            texture2d.SetPixel(i, 0, Color.green);
                        }
                        else
                        {
                            if (Time.frameCount % 2 == 0)
                                texture2d.SetPixel(i, 0, Color.blue);
                        }
                    }
                }
            }
        }

        texture2d.Apply();
        GameObject.Find("Image").GetComponent<RawImage>().texture = texture2d;
    }

    public void SaveData()
    {
        RootObject rootObject = new RootObject();
        rootObject.notes = notes;

        Debug.Log(JsonUtility.ToJson(rootObject));
    }

    public void LoadData()
    {
        String kalimbaSetup = "{\"notes\":[{\"caption\":\"C\",\"lowerBound\":85,\"upperBound\":93,\"thresholdValue\":0.025245806202292444},{\"caption\":\"D\",\"lowerBound\":95,\"upperBound\":105,\"thresholdValue\":0.024791114032268525},{\"caption\":\"E\",\"lowerBound\":107,\"upperBound\":117,\"thresholdValue\":0.03157658874988556},{\"caption\":\"F\",\"lowerBound\":118,\"upperBound\":124,\"thresholdValue\":0.026818973943591119},{\"caption\":\"G\",\"lowerBound\":128,\"upperBound\":139,\"thresholdValue\":0.03247590363025665},{\"caption\":\"A\",\"lowerBound\":146,\"upperBound\":156,\"thresholdValue\":0.05438023433089256},{\"caption\":\"B\",\"lowerBound\":164,\"upperBound\":174,\"thresholdValue\":0.0694345161318779},{\"caption\":\"C*\",\"lowerBound\":176,\"upperBound\":185,\"thresholdValue\":0.06816878169775009},{\"caption\":\"D*\",\"lowerBound\":196,\"upperBound\":206,\"thresholdValue\":0.07993388175964356},{\"caption\":\"E*\",\"lowerBound\":222,\"upperBound\":231,\"thresholdValue\":0.155356302857399},{\"caption\":\"F*\",\"lowerBound\":233,\"upperBound\":243,\"thresholdValue\":0.05957638472318649},{\"caption\":\"G*\",\"lowerBound\":264,\"upperBound\":275,\"thresholdValue\":0.10083159804344177},{\"caption\":\"A*\",\"lowerBound\":295,\"upperBound\":309,\"thresholdValue\":0.013621526770293713},{\"caption\":\"B*\",\"lowerBound\":331,\"upperBound\":342,\"thresholdValue\":0.009923440404236317},{\"caption\":\"C**\",\"lowerBound\":353,\"upperBound\":365,\"thresholdValue\":0.015800992026925088},{\"caption\":\"D**\",\"lowerBound\":392,\"upperBound\":404,\"thresholdValue\":0.02569706365466118},{\"caption\":\"E**\",\"lowerBound\":451,\"upperBound\":469,\"thresholdValue\":0.039687380194664}]}";

        RootObject rootObject = JsonUtility.FromJson<RootObject>(kalimbaSetup);
        notes = rootObject.notes;

        foreach (Note note in notes)
        {
            note.InitializeNote(thresholdSliderPanel, Instantiate(thresholdSliderPrefab, thresholdSliderPanel.transform), this);
        }

        RetriggerTimeoutValueChanged(retriggerTimeoutSlider);
        RetriggerLevelSliderChanged(retriggerLevelSlider);
    }

    public static float MapRange(float value, float inputRangeFrom, float inputRangeTo, float outputRangeFrom, float outputRangeTo)
    {
        return (value - inputRangeFrom) * (outputRangeTo - outputRangeFrom) / (inputRangeTo - inputRangeFrom) + outputRangeFrom;
    }

    int msSinceLastNote;

    public void triggerNote(string note)
    {
        if (outputInputField.text == "")
        {
            msSinceLastNote = Mathf.RoundToInt(Time.time * 1000);
            outputInputField.text = note + ", ";
        }
        else
        {
            outputInputField.text += Mathf.RoundToInt(Time.time * 1000) - msSinceLastNote + "; " + note + ", ";
            msSinceLastNote = Mathf.RoundToInt(Time.time * 1000);
        }
    }
}