using UnityEngine;
using System.IO;
using UnityEngine.Rendering;
using System.Threading.Tasks;

public class Main : MonoBehaviour
{
    private Gemini gemini;

    void Awake()
    {
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
        gemini = new Gemini(geminiProps, true);       
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        await gemini.Chat("Hello, Gemini!", "You are a helpful assistant.", null, null, null, null, (responseText) =>
        {
            Debug.Log("Gemini Response: " + responseText);
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
