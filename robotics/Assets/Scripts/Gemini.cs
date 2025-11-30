using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

/// <summary>
/// This is a C# port of a Godot project available on GitHub: https://github.com/araobp/airport
/// </summary>
public class Gemini
{
    // Configuration Struct
    public struct GeminiProps
    {
        public string GeminiModel;
        public string GeminiApiKey;
    }

    private string _apiEndpoint;
    private string _ttsApiEndpoint;
    private bool _enableHistory = false;
    private List<Content> _chatHistory = new List<Content>();
    private const int MAX_CHAT_HISTORY_LENGTH = 16;

    private string CHAT_HISTORY_LOG_PATH;

    // Default callback
    private void DefaultOutputText(string text)
    {
        Debug.Log($"DEFAULT OUTPUT: {text}\n\n");
    }

    #region Public Methods
    #endregion
    #region Public Methods

    /// <summary>
    /// Initializes a new instance of the <see cref="Gemini"/> class.
    /// </summary>
    public Gemini(GeminiProps props, bool enableHistory = false)
    {
        if (string.IsNullOrEmpty(props.GeminiModel))
        {
            props.GeminiModel = "gemini-2.5-flash";
        }
        string api = $"https://generativelanguage.googleapis.com/v1beta/models/{props.GeminiModel}:generateContent";
        Debug.Log($"Using Gemini API Endpoint: {api}");
        _apiEndpoint = $"{api}?key={props.GeminiApiKey}";
        _ttsApiEndpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash-preview-tts:generateContent?key={props.GeminiApiKey}";
        _enableHistory = enableHistory;
        CHAT_HISTORY_LOG_PATH = Path.Combine(Application.persistentDataPath, "chat_history.txt");
    }

    /// <summary>
    /// Main Chat function
    /// </summary>
    public async Task<string> Chat(
        string query, 
        string systemInstruction, 
        List<string> base64Images = null,
        JObject jsonSchema = null,
        JObject functionDeclarations = null,
        Dictionary<string, object> functionHandlers = null,
        Action<string> callback = null)
    {
        callback ??= DefaultOutputText;

        var conversation = new List<Content>(_chatHistory)
        {
            CreateUserContent(query, base64Images)
        };

        string latestResponseText = "";

        while (true)
        {
            var request = BuildRequest(conversation, systemInstruction, jsonSchema, functionDeclarations);
            var response = await SendRequest(request);

            if (response?.Candidates == null || !response.Candidates.Any())
            {
                Debug.LogError("Invalid response from Gemini API.");
                if (_enableHistory) _chatHistory.Clear();
                return null;
            }

            var modelResponseContent = response.Candidates[0].Content;
            conversation.Add(modelResponseContent);

            var functionCalls = modelResponseContent.Parts
                .Where(p => p.FunctionCall != null)
                .Select(p => p.FunctionCall)
                .ToList();

            foreach (var part in modelResponseContent.Parts.Where(p => !string.IsNullOrEmpty(p.Text)))
            {
                latestResponseText = part.Text;
                if (!latestResponseText.StartsWith("**")) // Don't output "thought" blocks
                {
                    callback(latestResponseText);
                }
            }

            if (!functionCalls.Any())
            {
                break; // End of conversation turn
            }

            // Execute all function calls and gather results
            foreach (var funcCall in functionCalls)
            {
                var functionResponseContent = await HandleFunctionCall(funcCall, functionHandlers);
                conversation.Add(functionResponseContent);
            }
        }

        if (_enableHistory)
        {
            _chatHistory = conversation;
            if (_chatHistory.Count > MAX_CHAT_HISTORY_LENGTH)
            {
                _chatHistory.RemoveRange(0, _chatHistory.Count - MAX_CHAT_HISTORY_LENGTH);
            }
        }

        LogHistory(conversation);

        return latestResponseText;
    }

    /// <summary>
    /// Synthesizes speech from text.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="voiceName">The name of the voice to use.</param>
    /// <returns>A byte array containing the synthesized audio data.</returns>
    public async Task<byte[]> SynthesizeSpeech(string text, string voiceName = "Leda")
    {
        var payload = new JObject();
        payload["contents"] = new JArray {
                new JObject {
                    ["parts"] = new JArray {
                        new JObject { ["text"] = text }
                    }
                }
            };
        payload["generationConfig"] = new JObject {
                ["responseModalities"] = new JArray { "AUDIO" },
                ["speechConfig"] = new JObject {
                    ["voiceConfig"] = new JObject {
                        ["prebuiltVoiceConfig"] = new JObject { ["voiceName"] = voiceName }
                    }
                }
            };
        //payload["model"] = "gemini-1.5-flash-preview-tts";

        string responseBody = await SendWebRequest(_ttsApiEndpoint, payload.ToString());
        if (string.IsNullOrEmpty(responseBody)) return null;

        try
        {
            var jsonResponse = JObject.Parse(responseBody);
            var audioData = jsonResponse["candidates"]?[0]?["content"]?["parts"]?[0]?["inlineData"]?["data"]?.ToString();

            if (string.IsNullOrEmpty(audioData)) return null;

            return Convert.FromBase64String(audioData);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON Parse Error or Base64 Decode Error: {ex.Message}\nResponse Body: {responseBody}");
            return null;
        }
    }


    #endregion

    #region Private Helpers

    private Content CreateUserContent(string query, List<string> base64Images)
    {
        var parts = new List<Part> { new Part { Text = query } };
        if (base64Images != null)
        {
            parts.AddRange(base64Images.Select(img => new Part
            {
                InlineData = new InlineData { MimeType = "image/jpeg", Data = img }
            }));
        }
        return new Content { Role = "user", Parts = parts.ToArray() };
    }

    private GeminiRequest BuildRequest(List<Content> conversation, string systemInstruction, JObject jsonSchema, JObject functionDeclarations)
    {
        var request = new GeminiRequest
        {
            SystemInstruction = new Content { Parts = new[] { new Part { Text = systemInstruction } } },
            Contents = conversation.ToArray(),
            GenerationConfig = new GenerationConfig()
        };

        if (jsonSchema != null)
        {
            request.GenerationConfig.ResponseMimeType = "application/json";
            request.GenerationConfig.ResponseSchema = jsonSchema;
        }

        if (functionDeclarations != null)
        {
            request.Tools = new[] { new Tool { FunctionDeclarations = functionDeclarations["functions"] } };
        }

        return request;
    }

    private async Task<GeminiResponse> SendRequest(GeminiRequest request)
    {
        var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver(), NullValueHandling = NullValueHandling.Ignore };
        string payload = JsonConvert.SerializeObject(request, settings);

        string responseBody = await SendWebRequest(_apiEndpoint, payload);
        if (string.IsNullOrEmpty(responseBody)) return null;

        try
        {
            return JsonConvert.DeserializeObject<GeminiResponse>(responseBody);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON Parse Error: {ex.Message}\nResponse Body: {responseBody}");
            return null;
        }
    }

    private async Task<Content> HandleFunctionCall(FunctionCall functionCall, Dictionary<string, object> functionHandlers)
    {
        string funcName = functionCall.Name;
        JObject args = JObject.FromObject(functionCall.Args);

        Debug.Log($"Calling: {funcName} with args: {args}\n\n");

        JObject functionResult = await ExecuteFunction(functionHandlers, funcName, args);

        return new Content
        {
            Role = "function",
            Parts = new[]
            {
                new Part
                {
                    FunctionResponse = new FunctionResponse
                    {
                        Name = funcName,
                        Response = functionResult
                    }
                }
            }
        };
    }

    private void LogHistory(List<Content> conversation)
    {
        try
        {
            Debug.Log($"Writing chat history log to: {CHAT_HISTORY_LOG_PATH}");
            string historyJson = JsonConvert.SerializeObject(conversation, Formatting.Indented);
            File.WriteAllText(CHAT_HISTORY_LOG_PATH, historyJson);
        }
        catch (Exception e)
        {
            Debug.LogError($"Cannot write log: {e.Message}");
        }
    }

    /// <summary>
    /// Helper to send UnityWebRequest asynchronously
    /// </summary>
    private async Task<string> SendWebRequest(string url, string jsonPayload)
    {
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            // request.SetRequestHeader("Accept-Encoding", "identity"); // Unity handles this automatically usually

            var operation = request.SendWebRequest();

            while (!operation.isDone) await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Gemini Error: {request.error}\nResponse: {request.downloadHandler.text}");
                return null;
            }

            return request.downloadHandler.text;
        }
    }

    /// <summary>
    ///  Executes a method on a referenced object within functionHandlers using Reflection
    /// </summary>
    private async Task<JObject> ExecuteFunction(Dictionary<string, object> functionHandlers, string funcName, JObject args)
    {
        if (functionHandlers == null || functionHandlers.Count == 0)
        {
            Debug.LogError("No function handlers provided.");
            return new JObject { ["error"] = "No function handlers available" };
        }

        // Find the method across all handler objects
        var (targetObject, method) = FindMethod(functionHandlers, funcName);

        if (method == null)
        {
            Debug.LogError($"Function '{funcName}' not found on any of the provided handler objects.");
            return new JObject { ["error"] = $"Function {funcName} not found" };
        }

        try
        {            
            object result = null;
            bool isAwaitable = method.ReturnType.GetMethod(nameof(Task.GetAwaiter)) != null;

            if (method.GetParameters().Length > 0)
            {
                result = method.Invoke(targetObject, new object[] { args });
            }
            else
            {
                result = method.Invoke(targetObject, null);
            }

            if (isAwaitable && result is Task task)
            {
                await task;
                if (task.GetType().IsGenericType)
                {
                    result = task.GetType().GetProperty("Result").GetValue(task, null);
                }
                else { result = null; } // Task with no result
            }
            
            return result is JObject jResult ? jResult : new JObject { ["result"] = JToken.FromObject(result) };
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error executing function {funcName}: {ex}");
            return new JObject { ["error"] = ex.Message };
        }
    }

    /// <summary>
    /// Finds a public method with the given name in any of the handler objects.
    /// </summary>
    private (object, MethodInfo) FindMethod(Dictionary<string, object> functionHandlers, string funcName)
    {
        foreach (var handler in functionHandlers.Values)
        {
            var method = handler.GetType().GetMethod(funcName, BindingFlags.Public | BindingFlags.Instance);
            if (method != null)
            {
                return (handler, method);
            }
        }
        return (null, null);
    }

    #endregion

    #region API Data Structures

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class GeminiRequest
    {
        public Content SystemInstruction { get; set; }
        public Content[] Contents { get; set; }
        public Tool[] Tools { get; set; }
        public GenerationConfig GenerationConfig { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class GeminiResponse
    {
        public Candidate[] Candidates { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class SynthesizeSpeechResponse
    {
        public Candidate[] Candidates { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Candidate
    {
        public Content Content { get; set; }
    }


    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Content
    {
        public string Role { get; set; }
        public Part[] Parts { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Part
    {
        public string Text { get; set; }
        public InlineData InlineData { get; set; }
        public FunctionCall FunctionCall { get; set; }
        public FunctionResponse FunctionResponse { get; set; }
        public AudioData AudioData { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class InlineData
    {
        public string MimeType { get; set; }
        public string Data { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class AudioData
    {
        public string MimeType { get; set; }
        public string Data { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class FunctionCall
    {
        public string Name { get; set; }
        public object Args { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class FunctionResponse
    {
        public string Name { get; set; }
        public object Response { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class Tool
    {
        public JToken FunctionDeclarations { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class GenerationConfig
    {
        public string ResponseMimeType { get; set; }
        public JObject ResponseSchema { get; set; }
        public ThinkingConfig ThinkingConfig { get; set; }
    }

    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public class ThinkingConfig
    {
        public bool IncludeThoughts { get; set; } = true;
    }

    #endregion
}