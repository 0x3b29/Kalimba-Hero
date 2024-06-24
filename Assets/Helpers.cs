using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Helpers
{
    // Method to map a float value to a color
    public static Color MapValueToColor(float value)
    {
        // Define the start (green) and end (red) colors
        Color blackColor = Color.black;
        Color greenColor = Color.green;
        Color redColor = Color.red;

        // Clamp the value between 0 and 0.02
        value = Mathf.Clamp(value, 0f, 0.02f);

        // Interpolate colors based on the value range
        if (value <= 0.01f)
        {
            // Interpolate from black to green
            float normalizedValue = value / 0.01f;
            return Color.Lerp(blackColor, greenColor, normalizedValue);
        }
        else
        {
            // Interpolate from green to red
            float normalizedValue = (value - 0.01f) / 0.01f;
            return Color.Lerp(greenColor, redColor, normalizedValue);
        }
    }

    public static float MapRange(float value, float inputRangeFrom, float inputRangeTo, float outputRangeFrom, float outputRangeTo)
    {
        // This line maps a value from one range to another
        return (value - inputRangeFrom) * (outputRangeTo - outputRangeFrom) / (inputRangeTo - inputRangeFrom) + outputRangeFrom;
    }
}
