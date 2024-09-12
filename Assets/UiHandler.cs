using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiHandler : MonoBehaviour
{
    [SerializeField] Manager manager;

    [SerializeField] TMP_Dropdown audioInputDevicesDropdown;
    [SerializeField] TMP_Text selectedNoteText;
    [SerializeField] TMP_Dropdown noteSelectorDropdown;

    [SerializeField] Button nextButton;
    [SerializeField] Button unselectButton;
    [SerializeField] Button previousButton;

    [SerializeField] Button addButton;
    [SerializeField] Button updateButton;

    [SerializeField] Button clearButton;
    [SerializeField] Button removeButton;

    [SerializeField] Button saveButton;
    [SerializeField] Button loadButton;

    [SerializeField] TMP_InputField noteNameInput;
    [SerializeField] TMP_InputField noteMidiInput;

    [SerializeField] Slider retriggerLevelSlider;
    [SerializeField] TMP_Text retriggerLevelText;

    [SerializeField] TMP_InputField outputInputField;
    [SerializeField] Button clearOutputButton;

    [SerializeField] Button closeButton;

    [SerializeField] Slider upperBoundSlider;
    [SerializeField] Slider lowerBoundSlider;

    public Action<string> selectedAudioInputDeviceChanged;
    public Action<string> selectedNoteChanged;

    public Action selectNextNote;
    public Action selectPreviousNote;
    public Action unselectNote;
    public Action<string, byte> addNewNote;
    public Action<string, byte> updateCurrentNote;
    public Action clearNotes;
    public Action removeNote;
    public Action loadDatasource;
    public Action saveDatasource;

    public Action screenResolutionChanged;
    Vector2 screenResolution;

    public Action<float> retriggerLevelChanged;

    public Action<float, float> bandValueChanged;

    int timeWhenLastNoteTriggeredInMS;

    // Start is called before the first frame update
    void Awake()
    {
        screenResolution = new Vector2(Screen.width, Screen.height);

        audioInputDevicesDropdown.onValueChanged.AddListener(OnAudioInputDevicesDropdownValueChanged);
        noteSelectorDropdown.onValueChanged.AddListener(NoteSelectorDropdownValueChanged);

        nextButton.onClick.AddListener(delegate { selectNextNote?.Invoke(); });
        previousButton.onClick.AddListener(delegate { selectPreviousNote?.Invoke(); });
        unselectButton.onClick.AddListener(delegate { unselectNote?.Invoke(); });

        addButton.onClick.AddListener(OnAddButtonClick);
        updateButton.onClick.AddListener(OnUpdateButtonClick);
        clearButton.onClick.AddListener(delegate { clearNotes?.Invoke(); });
        removeButton.onClick.AddListener(delegate { removeNote?.Invoke(); });

        saveButton.onClick.AddListener(delegate { saveDatasource?.Invoke(); });
        loadButton.onClick.AddListener(delegate { loadDatasource?.Invoke(); });

        retriggerLevelSlider.onValueChanged.AddListener(OnRetriggerLevelSliderValueChanged);

        clearOutputButton.onClick.AddListener(delegate { outputInputField.text = ""; });

        closeButton.onClick.AddListener(delegate { Application.Quit(); });

        upperBoundSlider.onValueChanged.AddListener(OnUpperBoundSliderValueChanged);
        lowerBoundSlider.onValueChanged.AddListener(OnLowerBoundSliderValueChanged);

        manager.audioDevicesListUpdated += OnInputDevicesListUpdated;
        manager.datasourceUpdated += OnDatasourceUpdated;
        manager.notesUpdated += OnNotesUpdated;
        manager.noteSelected += OnNoteSelected;
        manager.noteUpdated += OnNoteUpdated;

        manager.noteTriggered += OnNoteTriggered;
    }

    private void Update()
    {
        if (screenResolution.x != Screen.width || screenResolution.y != Screen.height)
        {
            screenResolutionChanged?.Invoke();

            screenResolution.x = Screen.width;
            screenResolution.y = Screen.height;
        }
    }

    void OnUpperBoundSliderValueChanged(float newUpperBoundValue)
    {
        // We want to make sure that the upperBoundSlider.value is at least lowerBoundSlider.value
        if (newUpperBoundValue < lowerBoundSlider.value)
        {
            lowerBoundSlider.value = newUpperBoundValue;
        }

        bandValueChanged?.Invoke(lowerBoundSlider.value, newUpperBoundValue);
    }

    void OnLowerBoundSliderValueChanged(float newLowerBoundValue)
    {
        // We want to make sure that the lowerBoundSlider.value is at most upperBoundSlider.value
        if (newLowerBoundValue > upperBoundSlider.value)
        {
            upperBoundSlider.value = newLowerBoundValue;
        }

        bandValueChanged?.Invoke(newLowerBoundValue, upperBoundSlider.value);
    }

    void OnRetriggerLevelSliderValueChanged(float newRetriggerLevel)
    {
        retriggerLevelChanged?.Invoke(newRetriggerLevel);
        retriggerLevelText.text = "Level for retrigger: " + (Mathf.Round(newRetriggerLevel * 100) / 100f);
    }

    void OnAddButtonClick()
    {
        byte midiValue = 0;
        byte.TryParse(noteMidiInput.text, out midiValue);

        addNewNote?.Invoke(noteNameInput.text, midiValue);
    }

    void OnUpdateButtonClick()
    {
        byte midiValue = 0;
        byte.TryParse(noteMidiInput.text, out midiValue);

        updateCurrentNote?.Invoke(noteNameInput.text, midiValue);
    }

    void OnNotesUpdated(List<Note> newNotes)
    {
        noteSelectorDropdown.options.Clear();

        foreach (Note note in newNotes)
        {
            noteSelectorDropdown.options.Add(new TMP_Dropdown.OptionData(note.caption));
        }

        noteSelectorDropdown.RefreshShownValue();
    }

    void OnNoteSelected(Note note)
    {
        if (note == null)
        {
            selectedNoteText.text = "/";
            noteNameInput.text = "";
            noteMidiInput.text = "";

            // If no note is selected, there is no need for the bounds sliders to be visible
            lowerBoundSlider.gameObject.SetActive(false);
            upperBoundSlider.gameObject.SetActive(false);

            return;
        }

        noteNameInput.text = note.caption;
        noteMidiInput.text = note.midiValue.ToString();

        // If a note is selected, we want the bounds sliders to be visible
        lowerBoundSlider.gameObject.SetActive(true);
        upperBoundSlider.gameObject.SetActive(true);

        lowerBoundSlider.value = note.GetLowerBound();
        upperBoundSlider.value = note.GetUpperBound();

        UpdateNoteCaption(note);
    }

    void OnNoteUpdated(Note note)
    {
        UpdateNoteCaption(note);
    }

    void UpdateNoteCaption(Note note)
    {
        selectedNoteText.text = note.caption + "(" + note.midiValue + ")";
    }

    void NoteSelectorDropdownValueChanged(int newSelectedIndex)
    {
        selectedNoteChanged?.Invoke(noteSelectorDropdown.options[newSelectedIndex].text);
    }

    void OnAudioInputDevicesDropdownValueChanged(int newSelectedIndex)
    {
        Debug.Log("OnInputDeviceDropdownValueChanged");
        selectedAudioInputDeviceChanged.Invoke(audioInputDevicesDropdown.options[newSelectedIndex].text);
    }

    void OnInputDevicesListUpdated(List<string> newListOfInputDevices)
    {
        Debug.Log("OnInputDevicesListUpdated");

        audioInputDevicesDropdown.options.Clear();

        foreach (var device in newListOfInputDevices)
        {
            audioInputDevicesDropdown.options.Add(new TMP_Dropdown.OptionData(device));
        }

        audioInputDevicesDropdown.value = 0;
        audioInputDevicesDropdown.RefreshShownValue();

        selectedAudioInputDeviceChanged.Invoke(audioInputDevicesDropdown.options[0].text);
    }

    void OnDatasourceUpdated(Datasource newDatasource)
    {
        Debug.Log("OnDatasourceUpdated");

        int inputDeviceIndex = 0;

        foreach (TMP_Dropdown.OptionData optionData in audioInputDevicesDropdown.options)
        {
            if (optionData.text.Equals(newDatasource.selectedAudioDevice))
            {
                inputDeviceIndex = audioInputDevicesDropdown.options.IndexOf(optionData);
            }
        }

        audioInputDevicesDropdown.value = inputDeviceIndex;
        audioInputDevicesDropdown.RefreshShownValue();

        selectedAudioInputDeviceChanged.Invoke(audioInputDevicesDropdown.options[inputDeviceIndex].text);

        retriggerLevelSlider.value = newDatasource.retriggerMinimumLevel;
    }

    public void OnNoteTriggered(Note note)
    {
        // This funciton is called from the notes to update the update the UI

        if (outputInputField.text == "")
        {
            // If the output field is empty, we set the triggered note and remember the time
            timeWhenLastNoteTriggeredInMS = Mathf.RoundToInt(Time.time * 1000);
            outputInputField.text = note.caption + ", ";
        }
        else
        {
            // If the output field already contains data, we append the difference between the time when last note triggered and now as well as the new note
            outputInputField.text += Mathf.RoundToInt(Time.time * 1000) - timeWhenLastNoteTriggeredInMS + "; " + note.caption + ", ";
            timeWhenLastNoteTriggeredInMS = Mathf.RoundToInt(Time.time * 1000);
        }
    }
}
