using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Datasource
{
    public int averageValues;
    public int retriggerTimeoutFrames;
    public float retriggerMinimumLevel;
    public List<Note> notes;

    public Datasource(int initialAverageValues, int initialRetriggerTimeoutFrames, float initialRetriggerMinimumLevel)
    {
        notes = new List<Note>();
        averageValues = initialAverageValues;
        retriggerTimeoutFrames = initialRetriggerTimeoutFrames;
        retriggerMinimumLevel = initialRetriggerMinimumLevel;
    }
}