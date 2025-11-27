using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

public class Gemini : MonoBehaviour
{
    // Configuration Struct
    public struct GeminiProps
    {
        public string GeminiModel;
        public string GeminiApiKey;
    }

    private string _apiEndpoint;
    private bool _enableHistory = false;
    private List<JObject> _chatHistory = new List<JObject>();
    
    private const int MAX_CHAT_HISTORY_LENGTH = 16;
    private const bool INCLUDE_THOUGHTS = true;
    
    // Path adjusted for Unity (PersistentDataPath is safer than res://)
    private string CHAT_HISTORY_LOG_PATH;

    // Default callback
    private void DefaultOutputText(string text)
    {
        Debug.Log($"DEFAULT OUTPUT: {text}\n\n");
    }

    /// <summary>
    /// Initialize the Gemini Instance
    /// </summary>
    public void Initialize(GeminiProps props, bool enableHistory = false)
    {
        string api = $"https://generativelanguage.googleapis.com/v1beta/models/{props.GeminiModel}:generateContent";
        Debug.Log($"Using Gemini API Endpoint: {api}");
        _apiEndpoint = $"{api}?key={props.GeminiApiKey}";
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
        Dictionary<string, object> functions = null, 
        Action<string> callback = null)
    {
        if (callback == null) callback = DefaultOutputText;

        string thoughtSignature = null;
        string responseText = "";

        // Workaround prompt injection from original script
        query += "\n\nAfter you have thought through the problem, please provide a concise final answer only when you have not provided it yet.";

        // Construct System Instruction
        var systemInstructionObj = new JObject
        {
            ["parts"] = new JArray { new JObject { ["text"] = systemInstruction } }
        };

        // Construct Content Parts
        var contentParts = new JArray();
        contentParts.Add(new JObject { ["text"] = query });

        if (base64Images != null)
        {
            foreach (var img in base64Images)
            {
                contentParts.Add(new JObject
                {
                    ["inline_data"] = new JObject
                    {
                        ["mime_type"] = "image/jpeg",
                        ["data"] = img
                    }
                });
            }
        }

        var content = new JObject
        {
            ["role"] = "user",
            ["parts"] = contentParts
        };

        // Handle History
        List<JObject> contents;
        if (_enableHistory)
        {
            _chatHistory.Add(content);
            if (_chatHistory.Count > MAX_CHAT_HISTORY_LENGTH)
            {
                int startIndex = Math.Max(0, _chatHistory.Count - MAX_CHAT_HISTORY_LENGTH);
                _chatHistory = _chatHistory.GetRange(startIndex, _chatHistory.Count - startIndex);
            }
            // Create a fresh list combining history and current content
            contents = new List<JObject>(_chatHistory); 
            // Note: content is already in _chatHistory, so we don't add it again to contents
        }
        else
        {
            contents = new List<JObject> { content };
        }

        // Construct Payload
        var payload = new JObject
        {
            ["system_instruction"] = systemInstructionObj,
            ["contents"] = JArray.FromObject(contents),
            ["generation_config"] = new JObject
            {
                ["thinking_config"] = new JObject
                {
                    ["thinking_budget"] = -1,
                    ["include_thoughts"] = INCLUDE_THOUGHTS
                }
            }
        };

        // JSON Schema
        if (jsonSchema != null)
        {
            payload["generation_config"]["response_mime_type"] = "application/json";
            payload["generation_config"]["response_schema"] = jsonSchema;
        }

        // Tools (Function Calling Definitions)
        if (functions != null && functions.ContainsKey("tools"))
        {
            payload["tools"] = JToken.FromObject(functions["tools"]);
        }

        // Main Loop for Function Calling / Multi-turn
        while (true)
        {
            // Perform Web Request
            string responseBody = await SendWebRequest(_apiEndpoint, payload.ToString());
            
            if (string.IsNullOrEmpty(responseBody)) return "Error or Empty Response";

            JObject jsonResponse;
            try
            {
                jsonResponse = JObject.Parse(responseBody);
            }
            catch (Exception ex)
            {
                Debug.LogError($"JSON Parse Error: {ex.Message}");
                return $"Error parsing response: {ex.Message}";
            }

            JToken candidate = jsonResponse["candidates"]?[0];
            if (candidate == null || candidate["content"] == null)
            {
                Debug.LogError($"No content in Gemini response: {jsonResponse}");
                if (_enableHistory) _chatHistory.Clear();
                return null;
            }

            JArray parts = (JArray)candidate["content"]["parts"];
            bool finishChatSession = true;

            var contentInRes = new JObject
            {
                ["role"] = "model",
                ["parts"] = parts
            };

            if (_enableHistory) _chatHistory.Add(contentInRes);
            
            // Note: In C#, we need to update the payload contents for the next loop iteration (if function calling happens)
            ((JArray)payload["contents"]).Add(contentInRes); 

            foreach (JObject part in parts)
            {
                // 1. Handle Text
                if (part.ContainsKey("text"))
                {
                    string txt = part["text"].ToString();
                    responseText = txt; // Store last text response
                    
                    // Not a "thought" block
                    if (!txt.StartsWith("**")) 
                    {
                        callback(txt);
                    }
                }

                // 2. Handle Thought Signature
                if (part.ContainsKey("thoughtSignature"))
                {
                    thoughtSignature = part["thoughtSignature"].ToString();
                }

                // 3. Handle Function Call
                if (part.ContainsKey("functionCall"))
                {
                    var functionCall = part["functionCall"];
                    string fullFuncName = functionCall["name"].ToString();
                    JObject args = (JObject)functionCall["args"];
                    
                    // Split <server_name>.<function_name> based on original logic split("_")
                    // Note: GDScript logic: name.split("_"). server is [0], func is join remainder.
                    var nameParts = fullFuncName.Split('_').ToList();
                    string serverName = nameParts[0];
                    nameParts.RemoveAt(0);
                    string funcName = string.Join("_", nameParts);

                    Debug.Log($"Calling: {serverName} -> {funcName} with args: {args}\n\n");

                    // Execute Local Function via Reflection
                    JObject functionResult = await ExecuteMcpFunction(functions, serverName, funcName, args);
                    
                    // Handle "as_content" logic from GDScript
                    JToken contentFromFunc = null;
                    if (args.ContainsKey("as_content") && (bool)args["as_content"] == true)
                    {
                        // Assuming ExecuteMcpFunction returns a JObject that might contain "content" and "result"
                        if (functionResult["content"] != null)
                        {
                            contentFromFunc = functionResult["content"];
                            functionResult = (JObject)functionResult["result"]; // unwrap
                        }
                    }

                    // Construct Function Response Part
                    var contentFuncResPart = new JObject
                    {
                        ["functionResponse"] = new JObject
                        {
                            ["name"] = fullFuncName,
                            ["response"] = functionResult
                        }
                    };

                    var contentFuncRes = new JObject
                    {
                        ["role"] = "function",
                        ["parts"] = new JArray { contentFuncResPart }
                    };

                    if (contentFromFunc != null)
                    {
                        ((JArray)contentFuncRes["parts"]).Add(contentFromFunc);
                    }

                    if (_enableHistory) _chatHistory.Add(contentFuncRes);
                    ((JArray)payload["contents"]).Add(contentFuncRes);

                    finishChatSession = false; // Loop again to send function result back to Gemini
                }
            }

            if (finishChatSession) break;
        }

        // Log History
        try
        {
            Debug.Log($"Writing chat history log to: {CHAT_HISTORY_LOG_PATH}");
            Debug.Log($"Chat History Payload: {payload["contents"]}");
            File.WriteAllText(CHAT_HISTORY_LOG_PATH, payload["contents"].ToString());
        }
        catch (Exception e)
        {
            Debug.LogError($"Cannot write log: {e.Message}");
        }

        // Logic from original: if callback was default, return text, else return empty string (as it was handled via callback)
        // However, in C# async tasks usually return the value regardless. 
        // Following original logic strictly:
        if (callback == DefaultOutputText) return responseText;
        return "";
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
    ///  Executes a method on a referenced object within mcpServers using Reflection
    /// </summary>
    private async Task<JObject> ExecuteMcpFunction(Dictionary<string, object> mcpServers, string serverName, string funcName, JObject args)
    {
        if (mcpServers == null || !mcpServers.ContainsKey("ref"))
        {
            Debug.LogError("MCP Servers 'ref' not found.");
            return new JObject { ["error"] = "No server references found" };
        }

        var refs = mcpServers["ref"] as Dictionary<string, object>;
        if (refs == null || !refs.ContainsKey(serverName))
        {
            Debug.LogError($"MCP Server '{serverName}' not found.");
            return new JObject { ["error"] = $"Server {serverName} not found" };
        }

        object targetObject = refs[serverName];
        MethodInfo method = targetObject.GetType().GetMethod(funcName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

        if (method == null)
        {
            Debug.LogError($"Function '{funcName}' not found on server '{serverName}'.");
            return new JObject { ["error"] = $"Function {funcName} not found" };
        }

        try
        {
            // Invoking the method. We assume the method takes a JObject (args) as a parameter.
            // If the target methods have specific signatures, you will need a parameter mapper here.
            // For this port, passing the JObject args directly is the closest match to GDScript's dynamic dictionary passing.
            
            object result = null;
            bool isAwaitable = method.ReturnType.GetMethod(nameof(Task.GetAwaiter)) != null;

            if (method.GetParameters().Length > 0)
                result = method.Invoke(targetObject, new object[] { args });
            else
                result = method.Invoke(targetObject, null);

            if (isAwaitable && result is Task<JObject> task)
            {
                return await task;
            }
            else if (result is JObject jRes)
            {
                return jRes;
            }
            
            // Fallback for simple types
            return JObject.FromObject(new { result = result });
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error executing function {funcName}: {ex}");
            return new JObject { ["error"] = ex.Message };
        }
    }
}