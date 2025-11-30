using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public class FunctionCalling : MonoBehaviour
{

    Gemini gemini;

    JObject functionDeclaration = JObject.Parse(@"
    {
        'name': 'SampleMethod',
        'description': 'A sample method that takes an integer and a string as parameters.',
        'parameters': {
            'type': 'object',
            'properties': {
                'a': {
                    'type': 'integer',
                    'description': 'An integer parameter.'
                },
                'b': {
                    'type': 'string',
                    'description': 'A string parameter.'
                }
            },
            'required': ['a', 'b']
        }
    }");

    Dictionary<string, object> functionHandlers = new Dictionary<string, object>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
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
        gemini = new Gemini(geminiProps, false);

        SampleClass sampleInstance = new SampleClass();
        functionHandlers.Add("SampleMethod", sampleInstance);

        await gemini.Chat("Call SampleMethod with a=42 and b='Hello from Gemini!'", // query
                "You are a helpful assistant that can call functions.",
                null, // base64Images,
                null,
                new JObject { ["functions"] = new JArray { functionDeclaration } }, // functionDeclarations
                functionHandlers, 
               (responseText) =>
            {
                Debug.Log("Gemini Response: " + responseText);
            }
        );
    }

    // Update is called once per frame
    void Update()
    {

    }
}
public class SampleClass
{
    public JObject SampleMethod(JObject args)
    {
        int a = args["a"].Value<int>();
        string b = args["b"].Value<string>();
        Debug.Log($"SampleMethod called from Gemini: a = {a}, b = '{b}'");

        return new JObject { ["result"] = $"Successfully called SampleMethod with a={a} and b='{b}'" };
    }
}
