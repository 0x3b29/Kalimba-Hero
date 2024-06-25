using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Datasource
{
    public float retriggerMinimumLevel;
    public string selectedAudioDevice;
    public int selectedAudioDeviceFrequency;
    public List<Note> notes;

    public Datasource(float initialRetriggerMinimumLevel)
    {
        notes = new List<Note>();
        retriggerMinimumLevel = initialRetriggerMinimumLevel;
    }
}