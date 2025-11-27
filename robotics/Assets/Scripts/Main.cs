using UnityEngine;
using System;
using System.IO;
using System.Threading.Tasks;

public class Main : MonoBehaviour
{
    private string _geminiApiKey;
    const string SYSTEM_INSTRUCTION = "You are a helpful assistant.";
    private Gemini _gemini;

    private void Awake()
    {
        LoadApiKey();
    }

    private void LoadApiKey()
    {
        string path = Path.Combine(Application.dataPath, "..", "gemini_api_key.txt");
        if (File.Exists(path))
        {
            _geminiApiKey = File.ReadAllText(path).Trim();
        }
        else
        {
            Debug.LogError($"API key file not found at: {path}. Please create this file and add your Gemini API key to it.");
            _geminiApiKey = null;
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async Task Test()
    {
        if (string.IsNullOrEmpty(_geminiApiKey)) return;
        Gemini.GeminiProps geminiProps = new Gemini.GeminiProps();
        geminiProps.GeminiModel = "gemini-2.5-flash";
        geminiProps.GeminiApiKey = _geminiApiKey;

        _gemini = new Gemini();
        _gemini.Initialize(geminiProps);

        Action<string> callback = (response) =>
        {
            Debug.Log($"Gemini Response: {response}");
        };

        await _gemini.Chat("Hello, Gemini!", SYSTEM_INSTRUCTION, null, null, null, callback);
    }


    async void Start()
    {
        await Test();
    }

    // Update is called once per frame
    void Update()
    {

    }
}
