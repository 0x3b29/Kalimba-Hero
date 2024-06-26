using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public enum NoteState
{
    notTriggered,
    rising,
    falling,
}

[Serializable]
public class Note
{
    // All the values that are public and have no getters / setters will be serialized
    public String caption;
    public int lowerBound;
    public int upperBound;
    public float thresholdValue;

    // All the values that are NonSerialized will be resetted after loading

    [field: NonSerialized] public NoteState noteState { get; private set; }
    [field: NonSerialized] public int framesSinceTriggered { get; set; }
    [field: NonSerialized] public float minRetriggerLevel { get; set; }
    [field: NonSerialized] public int lastTriggeredFrame { get; set; }

    float minValueSinceTriggered;
    float maxValueSinceTriggered;

    float thresholdSliderMaxValue;
    GameObject thresholdSliderPanel { get; set; }
    GameObject thresholdSliderParent { get; set; }
    Slider thresholdSlider { get; set; }
    Image thresholdBackgroundPanelImage { get; set; }
    SoundAnalyzer soundAnalyzer { get; set; }

    public Note(string caption, int lowerBound, int upperBound, float thresholdValue)
    {
        this.caption = caption;
        this.lowerBound = lowerBound;
        this.upperBound = upperBound;
        this.thresholdValue = thresholdValue;
    }

    public void InitializeNote(GameObject thresholdSliderPanel, GameObject thresholdSliderParent, SoundAnalyzer soundAnalyzer, float minRetriggerLevel)
    {
        // This function is executed after loading or creating of notes
        this.thresholdSliderPanel = thresholdSliderPanel;
        this.thresholdSliderParent = thresholdSliderParent;
        this.soundAnalyzer = soundAnalyzer;
        this.minRetriggerLevel = minRetriggerLevel;

        // The thresholdSliderParent has just been created, therefore set a usefull name
        thresholdSliderParent.name = caption + " " + " Threshold Slider";

        // Also set an initial position within the valid range
        SetThresholdSliderParentPosition();

        // And get the references to the slider and the background that we use as sound level
        thresholdSlider = thresholdSliderParent.GetComponentInChildren<Slider>();
        thresholdBackgroundPanelImage = thresholdSliderParent.transform.GetChild(0).transform.GetComponent<Image>();

        // We need set the maxValue to thresholdValue * 1.5f that there is some way to increase the thresholdValue with the slider
        this.thresholdSliderMaxValue = thresholdValue * 1.5f;
        thresholdSlider.maxValue = thresholdValue * 1.5f;
        thresholdSlider.value = thresholdValue;

        // Also we need to attach a listener to the notes slider
        thresholdSlider.onValueChanged.AddListener(delegate
        {
            TresholdSliderValueChanged(thresholdSlider);
        });

        noteState = NoteState.notTriggered;
    }

    public void TresholdSliderValueChanged(Slider thresholdSlider)
    {
        thresholdValue = thresholdSlider.value;
    }

    public void SetThresholdSliderParentPosition()
    {
        // Update the notes threshold slider position according to the lower bound, the upper bound and the width of the parent container
        Vector3 localPosition = thresholdSliderParent.GetComponent<RectTransform>().anchoredPosition;
        float containerWidth = thresholdSliderPanel.GetComponent<RectTransform>().rect.width;

        // Map the lower and upper bounds to positions
        float lowerPos = Helpers.MapRange(lowerBound, 0, 512, 0, containerWidth);
        float upperPos = Helpers.MapRange(upperBound, 0, 512, 0, containerWidth);

        // Then calculate the center of these two positions
        localPosition.x = lowerPos + ((upperPos - lowerPos) / 2);

        // Finally set the slider to this center
        thresholdSliderParent.GetComponent<RectTransform>().anchoredPosition = localPosition;
    }

    public void SetNewBounds(int newLowerBound, int newUpperBound)
    {
        // Make sure that the lower bound is max the upper bound
        if (newLowerBound > upperBound)
        {
            lowerBound = upperBound;
        }
        else
        {
            lowerBound = newLowerBound;
        }

        // Make sure that the upper bound is min the lower bound
        if (newUpperBound < lowerBound)
        {
            upperBound = lowerBound;
        }
        else
        {
            upperBound = newUpperBound;
        }

        // Update the threshold slider position
        SetThresholdSliderParentPosition();
    }

    public void IncFrameCounter()
    {
        if (noteState == NoteState.notTriggered)
        {
            return;
        }

        framesSinceTriggered++;
    }

    public void SetValue(float value, bool wasPeakInsideBounds)
    {
        // Sets the maximum sound level for the note 
        if (value > thresholdSliderMaxValue)
        {
            thresholdSliderMaxValue = value;
            thresholdSlider.maxValue = value;
        }

        // Set the threshold slider background
        thresholdBackgroundPanelImage.fillAmount = 1 / thresholdSliderMaxValue * value;

        if (noteState == NoteState.notTriggered && value > thresholdValue && wasPeakInsideBounds)
        {
            Debug.Log("Trigger");

            noteState = NoteState.rising;
            maxValueSinceTriggered = value;
            framesSinceTriggered = 0;
            lastTriggeredFrame = Time.frameCount;
            soundAnalyzer.UpdateUIForTriggeredNote(caption);

            return;
        }

        if (noteState != NoteState.notTriggered && value < (thresholdValue * 0.8f))
        {
            noteState = NoteState.notTriggered;
            framesSinceTriggered = 0;

            return;
        }

        if (noteState == NoteState.rising && value > maxValueSinceTriggered)
        {
            maxValueSinceTriggered = value;
            return;
        }

        if (noteState == NoteState.rising && value < maxValueSinceTriggered)
        {
            noteState = NoteState.falling;
            minValueSinceTriggered = value;

            return;
        }

        if (noteState == NoteState.falling && value < minValueSinceTriggered)
        {
            minValueSinceTriggered = value;

            return;
        }

        if (noteState == NoteState.falling && value > minValueSinceTriggered * minRetriggerLevel && wasPeakInsideBounds)
        {
            Debug.Log("Trigger");

            noteState = NoteState.rising;
            maxValueSinceTriggered = value;
            framesSinceTriggered = 0;
            lastTriggeredFrame = Time.frameCount;
            soundAnalyzer.UpdateUIForTriggeredNote(caption);

            return;
        }
    }

    public int GetLowerBound() { return lowerBound; }
    public int GetUpperBound() { return upperBound; }
}

