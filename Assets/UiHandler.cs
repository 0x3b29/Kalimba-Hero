using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class UiHandler : MonoBehaviour
{
    [SerializeField] SoundAnalyzer soundAnalyzer;
    [SerializeField] TMP_Dropdown audioInputDevicesDropdown;
    public Action<string> selectedAudioInputDeviceChanged;

    // Start is called before the first frame update
    void Awake()
    {
        audioInputDevicesDropdown.onValueChanged.AddListener(OnAudioInputDevicesDropdownValueChanged);
        soundAnalyzer.audioDevicesListUpdated += OnInputDevicesListUpdated;
        soundAnalyzer.datasourceUpdated += OnDatasourceUpdated;
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
