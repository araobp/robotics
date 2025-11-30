using UnityEngine;
using System.IO;
using UnityEngine.Rendering;
using System.Threading.Tasks;
using UnityEngine.UI;
using TMPro.EditorUtilities;
using NUnit.Framework;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class Main : MonoBehaviour
{
    private Gemini gemini;
    private AudioSource audioSource;

    [SerializeField] Button button;
    [SerializeField] TMP_Dropdown dropdown;
    [SerializeField] TMP_InputField inputField;


    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        string apiKey = "";
        string apiKeyPath = Path.Combine(Application.dataPath, "..", "gemini_api_key.txt");
        if (File.Exists(apiKeyPath))
        {
            apiKey = File.ReadAllText(apiKeyPath).Trim();
        }
        else
        {
            Debug.LogError($"API key file not found at: {apiKeyPath}. Please create this file and paste your API key in it.");
        }

        Gemini.GeminiProps geminiProps = new Gemini.GeminiProps
        {
            GeminiApiKey = apiKey,
            GeminiModel = "gemini-2.5-flash",
        };
        // Instantiate the Gemini class directly
        gemini = new Gemini(geminiProps, false);

        button.onClick.AddListener(startTTS);    
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        await gemini.Chat("Hello, Gemini!", "You are a helpful assistant.", null, null, null, null, (responseText) =>
        {
            Debug.Log("Gemini Response: " + responseText);
        });
    }

    public async void startTTS()
    {
        string selectedVoice = dropdown.options[dropdown.value].text;
        string inputText = inputField.text;

        await AudioTest(inputText, selectedVoice);
    }

    async Task AudioTest(string text = "This is a test of the text to speech synthesis.", string voice = "Sadachbia")
    {
        // Output PCM data, sampling rate: 24000Hz, 16-bit signed integer, mono.
        byte[] audioData = await gemini.SynthesizeSpeech(text, voice);

        if (audioData != null && audioData.Length > 0)
        {
            PlayAudio(audioData);
        }
        else
        {
            Debug.LogError("Synthesized audio data is null or empty.");
        }
    }

    void PlayAudio(byte[] pcmData)
    {
        // The synthesized audio is 16-bit PCM, so 2 bytes per sample.
        int samplesCount = pcmData.Length / 2;
        float[] floatData = new float[samplesCount];

        for (int i = 0; i < samplesCount; i++)
        {
            // Convert two bytes to a 16-bit signed integer (short)
            short sample = (short)((pcmData[i * 2 + 1] << 8) | pcmData[i * 2]);
            // Convert to float in the range -1.0 to 1.0
            floatData[i] = sample / 32768.0f;
        }

        const int sampleRate = 24000;
        const int channels = 1; // Mono

        AudioClip audioClip = AudioClip.Create("SynthesizedSpeech", samplesCount, channels, sampleRate, false);
        audioClip.SetData(floatData, 0);

        audioSource.clip = audioClip;
        audioSource.Play();
        Debug.Log("Playing synthesized speech.");
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
