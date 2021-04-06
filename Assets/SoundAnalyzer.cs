using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

[RequireComponent(typeof(AudioSource))]
public class SoundAnalyzer : MonoBehaviour
{
    public int qSamples = 8192; //1024;
    public int binSize = 8192; // you can change this up, I originally used 8192 for better resolution, but I stuck with 1024 because it was slow-performing on the phone

    float[] spectrum;
    int samplerate;

    public Text display; // drag a Text object here to display values
    public bool mute;
    public AudioMixer masterMixer; // drag an Audio Mixer here in the inspector

    int textWidth = 512;
    int textHeight = 200;
    Texture2D texture2d;

    Note selectedNote;
    List<Note> notes = new List<Note>();

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

    public GameObject thresholdSliderPrefab;
    public GameObject thresholdSliderParent;

    public Slider retriggerTimeoutSlider;
    public Slider retriggerLevelSlider;

    public Text retriggerTimeoutText;
    public Text retriggerLevelText;

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

        texture2d = new Texture2D(textWidth, textHeight);

        for (int x = 0; x < textWidth; x++)
        {
            for (int y = 0; y < textHeight; y++)
            {
                texture2d.SetPixel(x, y, Color.black);
            }
        }

        texture2d.Apply();

        GameObject.Find("Image").GetComponent<RawImage>().texture = texture2d;

        updateDropdownOptions();
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
        updateDropdownOptions();
        noteSelectorDropdown.value = notes.FindIndex(x => x == selectedNote);
    }

    void ClearButtonClick(Button clearButton)
    {
        notes.Clear();
        foreach(Transform child in thresholdSliderParent.transform)
        {
            Destroy(child.gameObject);
        }
        noteSelectorDropdown.options.Clear();
    }

    void NextButtonClick(Button nextButton)
    {
        if (selectedNote != null)
            Debug.Log("Selected was " + selectedNote.caption);
        else
            Debug.Log("Selected was null");

        if (selectedNote == null)
        {
            selectedNote = notes[0];
        }
        else if (selectedNote == notes[notes.Count - 1])
        {
            selectedNote = notes[0];
        }
        else
        {
            selectedNote = notes[notes.FindIndex(x => x == selectedNote) + 1];
        }

        noteSelectorDropdown.value = notes.FindIndex(x => x == selectedNote);
        Debug.Log("Selected is " + selectedNote.caption);
    }

    void PreviousButtonClick(Button previousButton)
    {
        if (selectedNote != null)
            Debug.Log("Selected was " + selectedNote.caption);
        else
            Debug.Log("Selected was null");

        if (selectedNote == null)
        {
            selectedNote = notes[notes.Count - 1];
        }
        else if (selectedNote == notes[0])
        {
            selectedNote = notes[notes.Count - 1];
        }
        else
        {
            selectedNote = notes[notes.FindIndex(x => x == selectedNote) - 1];
        }

        noteSelectorDropdown.value = notes.FindIndex(x => x == selectedNote);
        Debug.Log("Selected is " + selectedNote.caption);
    }

    void UnselectButtonClick(Button unselectButton)
    {
        selectedNote = null;
    }

    void AddButtonClick(Button addButton)
    {
        Note newNote = new Note(noteNameInput.text, 0, 0, 0);
        newNote.CreateThresholdSlider(Instantiate(thresholdSliderPrefab, thresholdSliderParent.transform));
        notes.Add(newNote);

        updateDropdownOptions();
        DropdownValueChanged(noteSelectorDropdown);

        RetriggerTimeoutValueChanged(retriggerTimeoutSlider);
        RetriggerLevelSliderChanged(retriggerLevelSlider);
    }

    void RemoveButtonClick(Button removeButton)
    {
        notes.Remove(selectedNote);
        updateDropdownOptions();
        DropdownValueChanged(noteSelectorDropdown);
    }

    void updateDropdownOptions()
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

        Debug.Log("New Value : " + change.value);
        selectedNote = notes[change.value];

        lowerBoundSlider.value = selectedNote.GetLowerBound();
        upperBoundSlider.value = selectedNote.GetUpperBound();
    }

    void UpperBoundSliderValueChanged(Slider upperBoundSlider)
    {
        if (selectedNote != null)
            selectedNote.SetUpperBound(Mathf.RoundToInt(upperBoundSlider.value));
    }

    void LowerBoundSliderValueChanged(Slider lowerBoundSlider)
    {
        if (selectedNote != null)
            selectedNote.SetLowerBound(Mathf.RoundToInt(lowerBoundSlider.value));
    }

    void Update()
    {
        for (int x = 0; x < textWidth; x++)
        {
            for (int y = textHeight; y > 0; y--)
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
            if (i < textWidth)
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
            note.CreateThresholdSlider(Instantiate(thresholdSliderPrefab, thresholdSliderParent.transform));
        }

        RetriggerTimeoutValueChanged(retriggerTimeoutSlider);
        RetriggerLevelSliderChanged(retriggerLevelSlider);
    }

    public static float MapRange(float value, float inputRangeFrom, float inputRangeTo, float outputRangeFrom, float outputRangeTo)
    {
        return (value - inputRangeFrom) * (outputRangeTo - outputRangeFrom) / (inputRangeTo - inputRangeFrom) + outputRangeFrom;
    }
}