using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using UnityEngine.UI;
using TMPro;
using Melanchall.DryWetMidi.Common;

public class MidiHandler : MonoBehaviour
{
    [SerializeField]
    TMP_Dropdown outputDeviceDropdown;
    string outputDeviceName;
    OutputDevice _outputDevice;

    // Start is called before the first frame update
    void Start()
    {
        outputDeviceDropdown.options.Add(new TMP_Dropdown.OptionData("None"));
        outputDeviceDropdown.value = 0;
        outputDeviceName = "None";
        outputDeviceDropdown.RefreshShownValue();

        foreach (OutputDevice outputDevice in OutputDevice.GetAll())
        {
            outputDeviceDropdown.options.Add(new TMP_Dropdown.OptionData(outputDevice.Name));
        }

        outputDeviceDropdown.onValueChanged.AddListener(delegate
        { OutputDeviceDropdownValueChanged(outputDeviceDropdown); });
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OutputDeviceDropdownValueChanged(TMP_Dropdown outputDeviceDropdown)
    {
        if (outputDeviceDropdown.options[outputDeviceDropdown.value].text != outputDeviceName)
        {
            outputDeviceName = outputDeviceDropdown.options[outputDeviceDropdown.value].text;

            if (_outputDevice != null)
            {
                _outputDevice.EventSent -= OnEventSent;
                _outputDevice.Dispose();
            }

            if (outputDeviceName == "None")
            {
                return;
            }

            _outputDevice = OutputDevice.GetByName(outputDeviceName);
            _outputDevice.EventSent += OnEventSent;
        }
    }

    public void SendNoteOnEvent(byte note, byte velocity)
    {
        if (_outputDevice == null)
        {
            return;
        }

        SevenBitNumber midiNote = new SevenBitNumber(note);
        SevenBitNumber midiVelocity = new SevenBitNumber(velocity);

        _outputDevice.SendEvent(new NoteOnEvent(midiNote, midiVelocity));

        StartCoroutine(SendNoteOffEventAfter1Second(note, velocity));
    }

    IEnumerator SendNoteOffEventAfter1Second(byte note, byte velocity)
    {
        yield return new WaitForSeconds(1);

        if (_outputDevice == null)
        {
            yield break;
        }

        SendNoteOffEvent(note, velocity);
    }

    public void SendNoteOffEvent(byte note, byte velocity)
    {
        SevenBitNumber midiNote = new SevenBitNumber(note);
        SevenBitNumber midiVelocity = new SevenBitNumber(velocity);

        _outputDevice.SendEvent(new NoteOffEvent(midiNote, midiVelocity));
    }

    void OnEventSent(object sender, MidiEventSentEventArgs e)
    {
        var midiDevice = (MidiDevice)sender;
        // Console.WriteLine($"Event sent to '{midiDevice.Name}' at {DateTime.Now}: {e.Event}");
    }

    private void OnDestroy()
    {
        if (_outputDevice == null)
        { 
            return; 
        }

        _outputDevice.EventSent -= OnEventSent;
        _outputDevice.Dispose();
        _outputDevice = null;

    }
}
