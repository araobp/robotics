using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading.Tasks;

[AddComponentMenu("Image Recognition Test")]
public class ImageRecognitionTest : MonoBehaviour
{
    private Gemini gemini;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    async void Start()
    {
        string apiKey = ApiKeyManager.GetApiKey();
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogError("API key not found or empty. Please create a 'gemini_api_key.txt' file in your project's root folder and paste your API key in it.");
            return;
        }

        Gemini.GeminiProps geminiProps = new Gemini.GeminiProps
        {
            GeminiApiKey = apiKey,
            GeminiModel = "gemini-2.5-flash",
        };
        gemini = new Gemini(geminiProps, false);        // Define the names of the two JPEG images in the Resources folder (without extension)
        string[] imageNames = { "BritishHouseYokohama", "TodorokiGreenSpace" };

        List<string> base64Images = new List<string>();

        foreach (string imageName in imageNames)
        {
            // Load the texture from the Resources folder
            var sourceTexture = Resources.Load<Texture2D>(imageName);

            if (sourceTexture != null)
            {
                // Create a temporary RenderTexture
                RenderTexture renderTex = RenderTexture.GetTemporary(
                    sourceTexture.width,
                    sourceTexture.height,
                    0,
                    RenderTextureFormat.Default,
                    RenderTextureReadWrite.Linear);

                // Blit the source texture to the RenderTexture
                Graphics.Blit(sourceTexture, renderTex);

                // Create a new readable Texture2D to copy the data to
                Texture2D readableTexture = new Texture2D(sourceTexture.width, sourceTexture.height);
                RenderTexture.active = renderTex;
                readableTexture.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
                readableTexture.Apply();

                // Encode the readable texture to JPG
                byte[] imageBytes = readableTexture.EncodeToJPG();
                string base64String = Convert.ToBase64String(imageBytes);
                base64Images.Add(base64String);
                Debug.Log($"Successfully loaded and converted '{imageName}'. Base64 string length: {base64String.Length}");

                RenderTexture.ReleaseTemporary(renderTex);
            }            else
            {
                Debug.LogWarning($"Could not find image '{imageName}' in any Resources folder.");
            }
        }

        if (base64Images.Count > 0)
        {
            string query = "Describe these images in detail.";
            string systemInstruction = "You are an expert at analyzing and describing images.";
            await gemini.Chat(query, systemInstruction, base64Images, null, null, null, (responseText) =>
            {
                Debug.Log("Gemini Response: " + responseText);
            });
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
