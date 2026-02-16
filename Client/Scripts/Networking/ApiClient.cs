using Godot;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProjectTactics.Networking;

/// <summary>
/// HTTP client for communicating with the Flask backend.
/// Handles auth tokens, JSON serialization, and all API calls.
///
/// Setup: Add as Autoload in Project Settings → Autoload
///        Name: "ApiClient", Path: res://Scripts/Networking/ApiClient.cs
/// </summary>
public partial class ApiClient : Node
{
	public static ApiClient Instance { get; private set; }

	// ─── CONFIGURATION ──────────────────────────────────────
	private const string BaseUrl = "http://localhost:5000/api";

	// ─── AUTH STATE ─────────────────────────────────────────
	public string AuthToken { get; private set; } = "";
	public string AccountId { get; private set; } = "";
	public string Username { get; private set; } = "";
	public bool IsLoggedIn => !string.IsNullOrEmpty(AuthToken);
	public bool IsAdmin { get; private set; } = false;

	// ─── SIGNALS ────────────────────────────────────────────
	[Signal] public delegate void LoginSuccessEventHandler(string username);
	[Signal] public delegate void LoginFailedEventHandler(string error);
	[Signal] public delegate void RegisterSuccessEventHandler(string username);
	[Signal] public delegate void RegisterFailedEventHandler(string error);

	// ─── JSON OPTIONS ───────────────────────────────────────
	private static readonly JsonSerializerOptions JsonOpts = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
		PropertyNameCaseInsensitive = true,
	};

	public override void _Ready()
	{
		if (Instance != null) { QueueFree(); return; }
		Instance = this;
		GD.Print("[ApiClient] Initialized. Backend: " + BaseUrl);
	}

	// ═════════════════════════════════════════════════════════
	//  HTTP HELPERS
	// ═════════════════════════════════════════════════════════

	private HttpRequest CreateRequest()
	{
		var req = new HttpRequest();
		AddChild(req);
		return req;
	}

	/// <summary>
	/// Make an HTTP request and return the parsed JSON response.
	/// </summary>
	private async Task<ApiResponse> RequestAsync(
		string method, string endpoint, object body = null)
	{
		var req = CreateRequest();
		string url = BaseUrl + endpoint;

		string[] headers = string.IsNullOrEmpty(AuthToken)
			? new[] { "Content-Type: application/json" }
			: new[] { "Content-Type: application/json", $"Authorization: Bearer {AuthToken}" };

		string bodyJson = body != null ? JsonSerializer.Serialize(body, JsonOpts) : "";

		HttpClient.Method httpMethod = method.ToUpper() switch
		{
			"GET" => HttpClient.Method.Get,
			"POST" => HttpClient.Method.Post,
			"PUT" => HttpClient.Method.Put,
			"DELETE" => HttpClient.Method.Delete,
			_ => HttpClient.Method.Get,
		};

		Error err;
		if (httpMethod == HttpClient.Method.Get || string.IsNullOrEmpty(bodyJson))
		{
			err = req.Request(url, headers, httpMethod);
		}
		else
		{
			err = req.Request(url, headers, httpMethod, bodyJson);
		}

		if (err != Error.Ok)
		{
			req.QueueFree();
			return new ApiResponse { Success = false, Error = $"Request failed: {err}" };
		}

		// Wait for completion
		var result = await ToSignal(req, HttpRequest.SignalName.RequestCompleted);

		long resultCode = (long)result[0];
		long responseCode = (long)result[1];
		string[] responseHeaders = (string[])result[2];
		byte[] responseBody = (byte[])result[3];

		req.QueueFree();

		string responseText = Encoding.UTF8.GetString(responseBody);

		if (responseCode >= 200 && responseCode < 300)
		{
			return new ApiResponse
			{
				Success = true,
				StatusCode = (int)responseCode,
				Body = responseText,
			};
		}
		else
		{
			// Try to extract error message from JSON
			string errorMsg = $"HTTP {responseCode}";
			try
			{
				using var doc = JsonDocument.Parse(responseText);
				if (doc.RootElement.TryGetProperty("error", out var errorProp))
				{
					errorMsg = errorProp.ValueKind == JsonValueKind.Array
						? string.Join(", ", errorProp.EnumerateArray())
						: errorProp.GetString();
				}
			}
			catch { }

			return new ApiResponse
			{
				Success = false,
				StatusCode = (int)responseCode,
				Body = responseText,
				Error = errorMsg,
			};
		}
	}

	// ═════════════════════════════════════════════════════════
	//  AUTH ENDPOINTS
	// ═════════════════════════════════════════════════════════

	public async Task<ApiResponse> Register(string username, string email, string password)
	{
		var resp = await RequestAsync("POST", "/auth/register", new
		{
			username,
			email,
			password,
		});

		if (resp.Success)
		{
			ParseAuthResponse(resp.Body);
			EmitSignal(SignalName.RegisterSuccess, Username);
			GD.Print($"[ApiClient] Registered as {Username}");
		}
		else
		{
			EmitSignal(SignalName.RegisterFailed, resp.Error);
			GD.PrintErr($"[ApiClient] Register failed: {resp.Error}");
		}

		return resp;
	}

	public async Task<ApiResponse> Login(string username, string password)
	{
		var resp = await RequestAsync("POST", "/auth/login", new
		{
			username,
			password,
		});

		if (resp.Success)
		{
			ParseAuthResponse(resp.Body);
			EmitSignal(SignalName.LoginSuccess, Username);
			GD.Print($"[ApiClient] Logged in as {Username}");
		}
		else
		{
			EmitSignal(SignalName.LoginFailed, resp.Error);
			GD.PrintErr($"[ApiClient] Login failed: {resp.Error}");
		}

		return resp;
	}

	public void Logout()
	{
		AuthToken = "";
		AccountId = "";
		Username = "";
		IsAdmin = false;
		GD.Print("[ApiClient] Logged out.");
	}

	private void ParseAuthResponse(string json)
	{
		using var doc = JsonDocument.Parse(json);
		var root = doc.RootElement;

		AuthToken = root.GetProperty("token").GetString();

		var account = root.GetProperty("account");
		AccountId = account.GetProperty("id").GetString();
		Username = account.GetProperty("username").GetString();
		IsAdmin = account.GetProperty("is_admin").GetBoolean();
	}

	// ═════════════════════════════════════════════════════════
	//  CHARACTER ENDPOINTS
	// ═════════════════════════════════════════════════════════

	/// <summary>Get all character slots for the logged-in account.</summary>
	public async Task<ApiResponse> GetCharacters()
	{
		return await RequestAsync("GET", "/characters/");
	}

	/// <summary>Create a new character.</summary>
	public async Task<ApiResponse> CreateCharacter(string name, string city, string bio, int slot)
	{
		return await RequestAsync("POST", "/characters/", new
		{
			name,
			city,
			bio,
			slot,
		});
	}

	/// <summary>Get full character data by ID.</summary>
	public async Task<ApiResponse> GetCharacter(string characterId)
	{
		return await RequestAsync("GET", $"/characters/{characterId}");
	}

	/// <summary>Update character data (save).</summary>
	public async Task<ApiResponse> UpdateCharacter(string characterId, object data)
	{
		return await RequestAsync("PUT", $"/characters/{characterId}", data);
	}

	/// <summary>Delete a character.</summary>
	public async Task<ApiResponse> DeleteCharacter(string characterId)
	{
		return await RequestAsync("DELETE", $"/characters/{characterId}");
	}

	/// <summary>Train a stat (server-authoritative).</summary>
	public async Task<ApiResponse> TrainStat(string characterId, string stat, int points = 1)
	{
		return await RequestAsync("POST", $"/characters/{characterId}/train", new
		{
			stat,
			points,
		});
	}

	/// <summary>Check if a character name is available.</summary>
	public async Task<ApiResponse> CheckName(string name)
	{
		return await RequestAsync("GET", $"/characters/check-name?name={Uri.EscapeDataString(name)}");
	}

	// ═════════════════════════════════════════════════════════
	//  ADMIN ENDPOINTS
	// ═════════════════════════════════════════════════════════

	public async Task<ApiResponse> AdminSetRank(string characterId, string rank)
	{
		return await RequestAsync("POST", $"/admin/character/{characterId}/set-rank", new { rank });
	}

	public async Task<ApiResponse> AdminGrantStats(string characterId, object grants)
	{
		return await RequestAsync("POST", $"/admin/character/{characterId}/grant-stats", new { grants });
	}
}

// ═════════════════════════════════════════════════════════════
//  RESPONSE WRAPPER
// ═════════════════════════════════════════════════════════════

public class ApiResponse
{
	public bool Success { get; set; }
	public int StatusCode { get; set; }
	public string Body { get; set; } = "";
	public string Error { get; set; } = "";

	/// <summary>Parse the response body as a JsonDocument.</summary>
	public JsonDocument GetJson()
	{
		return JsonDocument.Parse(Body);
	}
}
