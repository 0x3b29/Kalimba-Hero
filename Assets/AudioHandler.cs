using UnityEngine;
using UnityEngine.Audio;
using System.Collections;
using System.Linq;
using System;

public enum SpectrumSize
{
    s_128 = 128,
    s_256 = 256,
    s_512 = 512,
    s_1024 = 1024,
    s_2048 = 2048,
    s_4096 = 4096,
    s_8192 = 8192,
}

public class AudioHandler : MonoBehaviour
{
    [SerializeField] bool forceResyncDuringNextUpdate = false;
    [SerializeField] float currentPlaybackOffset;
    [SerializeField] float maxPlaybackOffset = 0.25f;

    [SerializeField] FFTWindow fftWindow;
    [SerializeField] SpectrumSize spectrumSize = SpectrumSize.s_8192;
    SpectrumSize oldSpectrumSize;

    float[] spectrum;

    [SerializeField] AudioMixer mainMixer;
    [SerializeField] AudioSource audioSource;

    string currentInputDeviceName;
    int currentInputDeviceFrequency;

    void Start()
    {
        spectrum = new float[((int)spectrumSize)];
        oldSpectrumSize = spectrumSize;
    }

    public void UpdateAudioSource(string inputDeviceName)
    {
        StartCoroutine(UpdateAudioSourceAsync(inputDeviceName));
    }

    IEnumerator UpdateAudioSourceAsync(string inputDeviceName)
    {
        if (!Microphone.devices.Any(x => x.Equals(inputDeviceName)))
        {
            throw new Exception("Device not found: '" + inputDeviceName + "'");
        }
        currentInputDeviceName = inputDeviceName;

        Microphone.GetDeviceCaps(inputDeviceName, out int minInputDeviceFrequency, out int inputDeviceFrequency);
        currentInputDeviceFrequency = inputDeviceFrequency;

        if (audioSource.clip)
        {
            audioSource.Stop();
            audioSource.clip.UnloadAudioData();
            Destroy(audioSource.clip);
            yield return null;
        }

        audioSource.clip = Microphone.Start(inputDeviceName, true, 10, inputDeviceFrequency);
        audioSource.loop = true;

        int tries = 0;
        while (Microphone.GetPosition(inputDeviceName) < 0) 
        { 
            yield return null; 
            if (tries > 100) { 
                Debug.Log("Did not start"); 
                break; 
            } 
            tries++; 
        }

        yield return null;

        float position = Microphone.GetPosition(inputDeviceName) / (float) inputDeviceFrequency;

        StartCoroutine(SyncAudioSourceWithMicrophoneAsync(position));
    }

    IEnumerator SyncAudioSourceWithMicrophoneAsync(float microphonePosition)
    {
        audioSource.Stop();
        yield return null;
        audioSource.time = microphonePosition;
        yield return null;
        audioSource.Play();
    }

    void Update()
    {
        float audioSourceTime = audioSource.time;
        float microphonePosition = Microphone.GetPosition(currentInputDeviceName) / (float)currentInputDeviceFrequency;

        currentPlaybackOffset = audioSourceTime - microphonePosition;

        if (audioSource.isPlaying && (forceResyncDuringNextUpdate || Mathf.Abs(currentPlaybackOffset) > maxPlaybackOffset))
        {
            forceResyncDuringNextUpdate = false;
            Debug.Log("Resynced! AudioSource: " + audioSourceTime + 
                " Microphone: " + microphonePosition + 
                " Delta: " + currentPlaybackOffset);
            StartCoroutine(SyncAudioSourceWithMicrophoneAsync(microphonePosition));
        }

        if (oldSpectrumSize != spectrumSize)
        {
            spectrum = new float[(int)spectrumSize];
            oldSpectrumSize = spectrumSize;
        }
    }

    public float[] GetSpectrumData()
    {
        // Now we get the most updated audio spectrum data
        audioSource.GetSpectrumData(spectrum, 0, fftWindow);

        return spectrum;
    }
}