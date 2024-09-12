using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiHandler : MonoBehaviour
{
    [SerializeField] SoundAnalyzer soundAnalyzer;

    [SerializeField] TMP_Dropdown audioInputDevicesDropdown;
    [SerializeField] TMP_Text selectedNoteText;
    [SerializeField] TMP_Dropdown noteSelectorDropdown;

    [SerializeField] Button nextButton;
    [SerializeField] Button unselectButton;
    [SerializeField] Button previousButton;

    [SerializeField] Button addButton;
    [SerializeField] Button updateButton;

    [SerializeField] TMP_InputField noteNameInput;
    [SerializeField] TMP_InputField noteMidiInput;

    public Action<string> selectedAudioInputDeviceChanged;
    public Action<string> selectedNoteChanged;

    public Action selectNextNote;
    public Action selectPreviousNote;
    public Action unselectNote;
    public Action<string, byte> addNewNote;
    public Action<string, byte> updateCurrentNote;

    public Action screenResolutionChanged;
    Vector2 screenResolution;

    // Start is called before the first frame update
    void Awake()
    {
        // We initially save the screen resolution to be later able to reacto to resize events
        screenResolution = new Vector2(Screen.width, Screen.height);

        audioInputDevicesDropdown.onValueChanged.AddListener(OnAudioInputDevicesDropdownValueChanged);
        noteSelectorDropdown.onValueChanged.AddListener(NoteSelectorDropdownValueChanged);

        nextButton.onClick.AddListener(delegate { selectNextNote?.Invoke(); });
        previousButton.onClick.AddListener(delegate { selectPreviousNote?.Invoke(); });
        unselectButton.onClick.AddListener(delegate { unselectNote?.Invoke(); });

        addButton.onClick.AddListener(OnAddButtonClick);
        updateButton.onClick.AddListener(OnUpdateButtonClick);

        soundAnalyzer.audioDevicesListUpdated += OnInputDevicesListUpdated;
        soundAnalyzer.datasourceUpdated += OnDatasourceUpdated;
        soundAnalyzer.notesUpdated += OnNotesUpdated;
        soundAnalyzer.noteSelected += OnNoteSelected;
        soundAnalyzer.noteUpdated += OnNoteUpdated;
    }

    private void Update()
    {
        // First we check if the screen size changed
        if (screenResolution.x != Screen.width || screenResolution.y != Screen.height)
        {
            screenResolutionChanged?.Invoke();

            // And remember the resolution for next frame
            screenResolution.x = Screen.width;
            screenResolution.y = Screen.height;
        }
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
            return;
        }

        noteNameInput.text = note.caption;
        noteMidiInput.text = note.midiValue.ToString();
        
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
    }
}
