using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class Note
{
    // All the values that are public and have no getters / setters will be serialized
    public String caption;
    public int lowerBound;
    public int upperBound;
    public float thresholdValue;

    // All the values that are NonSerialized will be resetted after loading
    [field: NonSerialized] public bool triggered { get; set; }
    [field: NonSerialized] public int numberOfValuesToAverage { get; set; }
    [field: NonSerialized] public int minRetriggerTimeoutFrames { get; set; }
    [field: NonSerialized] public float minRetriggerLevel { get; set; }

    [field: NonSerialized] private float maxValue { get; set; }
    [field: NonSerialized] private float oldValue { get; set; }
    [field: NonSerialized] private Queue<float> values { get; set; }

    [field: NonSerialized] public int lastTriggeredFrame { get; set; }

    [field: NonSerialized] private GameObject thresholdSliderPanel { get; set; }
    [field: NonSerialized] private GameObject thresholdSliderParent { get; set; }
    [field: NonSerialized] private Slider thresholdSlider { get; set; }
    [field: NonSerialized] private Image thresholdBackgroundPanelImage { get; set; }
    [field: NonSerialized] private SoundAnalyzer soundAnalyzer { get; set; }

    public Note(string caption, int lowerBound, int upperBound, float thresholdValue)
    {
        this.caption = caption;
        this.lowerBound = lowerBound;
        this.upperBound = upperBound;
        this.thresholdValue = thresholdValue;
    }

    public void InitializeNote(GameObject thresholdSliderPanel, GameObject thresholdSliderParent, SoundAnalyzer soundAnalyzer, int minFramesBeforRegister, int minRetriggerTimeoutFrames, float minRetriggerLevel)
    {
        // This function is executed after loading or creating of notes
        this.thresholdSliderPanel = thresholdSliderPanel;
        this.thresholdSliderParent = thresholdSliderParent;
        this.soundAnalyzer = soundAnalyzer;
        this.numberOfValuesToAverage = minFramesBeforRegister;
        this.minRetriggerTimeoutFrames = minRetriggerTimeoutFrames;
        this.minRetriggerLevel = minRetriggerLevel;

        // The thresholdSliderParent has just been created, therefore set a usefull name
        thresholdSliderParent.name = caption + " " + " Threshold Slider";

        // Also set an initial position within the valid range
        SetThresholdSliderParentPosition();

        // And get the references to the slider and the background that we use as sound level
        thresholdSlider = thresholdSliderParent.GetComponentInChildren<Slider>();
        thresholdBackgroundPanelImage = thresholdSliderParent.transform.GetChild(0).transform.GetComponent<Image>();

        // We need set the maxValue to thresholdValue * 1.5f that there is some way to increase the thresholdValue with the slider
        this.maxValue = thresholdValue * 1.5f;
        thresholdSlider.maxValue = thresholdValue * 1.5f;
        thresholdSlider.value = thresholdValue;

        // Also we need to attach a listener to the notes slider
        thresholdSlider.onValueChanged.AddListener(delegate {
            TresholdSliderValueChanged(thresholdSlider);
        });

        values = new Queue<float>();
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

    public void SetValue(float value)
    {
        // Put the new value in the values queue
        values.Enqueue(value);

        // Make sure there are at max as many values in the queue as we need
        while (values.Count > numberOfValuesToAverage)
            values.Dequeue();

        // Calculate the rolling average
        float averageValue = values.Sum() / values.Count;

        // Sets the maximum sound level for the note 
        if (value > maxValue)
        {
            maxValue = value;
            thresholdSlider.maxValue = value;
        }

        // Set the threshold slider background
        thresholdBackgroundPanelImage.fillAmount = 1 / maxValue * value;

        // Check if enough time has elapsed since this note has been triggered last time
        if (Time.frameCount > lastTriggeredFrame + minRetriggerTimeoutFrames)
        {
            if (triggered)
            {
                if (averageValue < thresholdValue)
                {
                    // If we drop below the trasholdvalue, the note will be set to not triggered
                    triggered = false;
                }
                else if (value > oldValue * minRetriggerLevel)
                {
                    // If the note is already in a triggered state, we need to check if the current level is higher than the previous level
                    lastTriggeredFrame = Time.frameCount;
                    soundAnalyzer.UpdateUIForTriggeredNote(caption);
                }
            }
            else
            {
                // If the note is not in the triggered state, we check if the current value has passed the threshold value
                if (averageValue > thresholdValue)
                {
                    triggered = true;
                    lastTriggeredFrame = Time.frameCount;
                    soundAnalyzer.UpdateUIForTriggeredNote(caption);
                }
            }
        }

        oldValue = value;
    }

    public int GetLowerBound() { return lowerBound; }
    public int GetUpperBound() { return upperBound; }
}

