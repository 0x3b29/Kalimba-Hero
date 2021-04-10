using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class RootObject
{
    public List<Note> notes;
}

[Serializable]
public class Note
{
    public String caption;
    public int lowerBound;
    public int upperBound;
    public float thresholdValue;

    public bool triggered { get; set; }
    public int minTimeoutFrames { get; set; }
    public float minLevelForRetrigger { get; set; }

    private float maxValue { get; set; }
    private float oldValue { get; set; }

    public int lastTriggeredFrame { get; set; }

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
        this.maxValue = thresholdValue * 1.5f;
    }

    public void InitializeNote(GameObject thresholdSliderPanel, GameObject thresholdSliderParent, SoundAnalyzer soundAnalyzer)
    {
        this.thresholdSliderPanel = thresholdSliderPanel;
        this.thresholdSliderParent = thresholdSliderParent;

        thresholdSliderParent.name = caption + " " + " Threshold Slider";

        SetThresholdSliderParentPosition(thresholdSliderPanel.GetComponent<RectTransform>().rect.width);

        thresholdSlider = thresholdSliderParent.GetComponentInChildren<Slider>();
        thresholdBackgroundPanelImage = thresholdSliderParent.transform.GetChild(0).transform.GetComponent<Image>();

        this.maxValue = thresholdValue * 1.5f;
        thresholdSlider.maxValue = thresholdValue * 1.5f;
        thresholdSlider.value = thresholdValue;

        thresholdSlider.onValueChanged.AddListener(delegate {
            TresholdSliderValueChanged(thresholdSlider);
        });

        this.soundAnalyzer = soundAnalyzer;
    }

    public void TresholdSliderValueChanged(Slider thresholdSlider)
    {
        thresholdValue = thresholdSlider.value;
    }

    public void SetThresholdSliderParentPosition(float containerWidth)
    {
        Vector3 localPosition = thresholdSliderParent.GetComponent<RectTransform>().anchoredPosition;

        float lowerPos = SoundAnalyzer.MapRange(lowerBound, 0, 512, 0, containerWidth);
        float upperPos = SoundAnalyzer.MapRange(upperBound, 0, 512, 0, containerWidth);

        localPosition.x = lowerPos + ((upperPos - lowerPos) / 2);
        thresholdSliderParent.GetComponent<RectTransform>().anchoredPosition = localPosition;
    }

    public void SetNewBounds(int newLowerBound, int newUpperBound)
    {
        if (newLowerBound > upperBound)
        {
            lowerBound = upperBound;
        }
        else
        {
            lowerBound = newLowerBound;
        }

        if (newUpperBound < lowerBound)
        {
            upperBound = lowerBound;
        }
        else
        {
            upperBound = newUpperBound;
        }

        SetThresholdSliderParentPosition(thresholdSliderPanel.GetComponent<RectTransform>().rect.width);
    }

    public void SetValue(float value)
    {
        if (value > maxValue)
        {
            maxValue = value;
            thresholdSlider.maxValue = value;
        }

        thresholdBackgroundPanelImage.fillAmount = 1 / maxValue * value;

        if (Time.frameCount > lastTriggeredFrame + minTimeoutFrames)
        {
            if (triggered)
            {
                if (value > thresholdValue && value > oldValue * minLevelForRetrigger)
                {
                    Debug.Log(caption + " got re-triggered.");
                    lastTriggeredFrame = Time.frameCount;
                    soundAnalyzer.triggerNote(caption);
                }

                if (value <= thresholdValue)
                {
                    triggered = false;
                }
            }
            else
            {
                if (value > oldValue && value > thresholdValue)
                {
                    Debug.Log(caption + " got triggered."); // by " + value + " which was more than " + thresholdValue);
                    triggered = true;
                    lastTriggeredFrame = Time.frameCount;
                    soundAnalyzer.triggerNote(caption);
                }
            }
        }

        oldValue = value;
    }

    public int GetLowerBound() { return lowerBound; }
    public int GetUpperBound() { return upperBound; }
}

