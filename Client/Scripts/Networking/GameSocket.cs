using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ProjectTactics.Networking;

/// <summary>
/// WebSocket client for Flask-SocketIO.
/// Handles real-time chat messaging and player position sync.
/// Add as Autoload: Name "GameSocket", Path res://Scripts/Networking/GameSocket.cs
///
/// Socket.IO over Engine.IO protocol:
///   0 = open, 2 = ping, 3 = pong, 4 = message
///   40 = connect, 42 = event, 43 = ack
/// </summary>
public partial class GameSocket : Node
{
	public static GameSocket Instance { get; private set; }

	// ─── SIGNALS ─────────────────────────────────────────────
	[Signal] public delegate void ConnectedEventHandler();
	[Signal] public delegate void DisconnectedEventHandler();
	[Signal] public delegate void ChatReceivedEventHandler(string sender, string senderId, string text, string type, string color);
	[Signal] public delegate void ChatErrorReceivedEventHandler(string error);
	[Signal] public delegate void PlayerJoinedEventHandler(string id, string name, string rank, string allegiance, float x, float y);
	[Signal] public delegate void PlayerMovedEventHandler(string id, float x, float y);
	[Signal] public delegate void PlayerLeftEventHandler(string id);

	// ─── STATE ───────────────────────────────────────────────
	private WebSocketPeer _ws;
	private bool _connected = false;
	private bool _sioConnected = false;
	private string _sid = "";
	private double _pingInterval = 25.0;
	private double _pingTimer = 0.0;
	private double _positionTimer = 0.0;
	private const double PositionSendRate = 0.1; // 100ms
	private Vector2 _lastSentPos = Vector2.Zero;
	private bool _joinedWorld = false;

	private string _serverUrl = "";
	private string _authToken = "";

	public bool IsConnected => _sioConnected;

	public override void _Ready()
	{
		if (Instance != null) { QueueFree(); return; }
		Instance = this;
		GD.Print("[GameSocket] Initialized.");
	}

	public override void _Process(double delta)
	{
		if (_ws == null) return;

		_ws.Poll();

		var state = _ws.GetReadyState();
		if (state == WebSocketPeer.State.Open)
		{
			while (_ws.GetAvailablePacketCount() > 0)
			{
				string msg = _ws.GetPacket().GetStringFromUtf8();
				HandleMessage(msg);
			}

			// Ping keepalive
			_pingTimer += delta;
			if (_pingTimer >= _pingInterval)
			{
				_pingTimer = 0;
				_ws.SendText("2");  // Engine.IO ping
			}

			// Position sync
			if (_joinedWorld)
			{
				_positionTimer += delta;
				if (_positionTimer >= PositionSendRate)
				{
					_positionTimer = 0;
					SendPositionIfMoved();
				}
			}
		}
		else if (state == WebSocketPeer.State.Closed)
		{
			if (_connected)
			{
				_connected = false;
				_sioConnected = false;
				_joinedWorld = false;
				EmitSignal(SignalName.Disconnected);
				GD.Print("[GameSocket] Disconnected.");
			}
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  PUBLIC API
	// ═════════════════════════════════════════════════════════════

	/// <summary>Connect to the SocketIO server.</summary>
	public void Connect(string baseUrl, string authToken)
	{
		_authToken = authToken;
		// Engine.IO handshake URL — Socket.IO uses /socket.io/ path
		// First we need to do an HTTP polling handshake, then upgrade to WS
		_serverUrl = baseUrl.Replace("http://", "ws://").Replace("https://", "wss://");
		if (_serverUrl.EndsWith("/api"))
			_serverUrl = _serverUrl[..^4]; // strip /api

		string wsUrl = $"{_serverUrl}/socket.io/?EIO=4&transport=websocket";

		_ws = new WebSocketPeer();
		var err = _ws.ConnectToUrl(wsUrl);
		if (err != Error.Ok)
		{
			GD.PrintErr($"[GameSocket] Failed to connect: {err}");
			return;
		}

		_connected = true;
		_pingTimer = 0;
		GD.Print($"[GameSocket] Connecting to {wsUrl}...");
	}

	/// <summary>Disconnect from the server.</summary>
	public void Disconnect()
	{
		if (_ws != null)
		{
			_ws.Close();
			_ws = null;
		}
		_connected = false;
		_sioConnected = false;
		_joinedWorld = false;
	}

	/// <summary>Join the overworld with the active character.</summary>
	public void JoinWorld(string characterId, Vector2 position)
	{
		if (!_sioConnected) return;

		var data = new Dictionary<string, object>
		{
			["character_id"] = characterId,
			["x"] = position.X,
			["y"] = position.Y,
		};
		SendEvent("join_world", data);
		_lastSentPos = position;
		_joinedWorld = true;
		GD.Print($"[GameSocket] Joined world as {characterId}");
	}

	/// <summary>Send a chat message through the socket.</summary>
	public void SendChat(string type, string text, string target = null, string color = null)
	{
		if (!_sioConnected) return;

		var data = new Dictionary<string, object>
		{
			["type"] = type,
			["text"] = text,
		};
		if (!string.IsNullOrEmpty(target)) data["target"] = target;
		if (!string.IsNullOrEmpty(color)) data["color"] = color;

		SendEvent("chat", data);
	}

	/// <summary>Send position update.</summary>
	public void SendPosition(Vector2 position)
	{
		if (!_sioConnected || !_joinedWorld) return;

		var data = new Dictionary<string, object>
		{
			["x"] = position.X,
			["y"] = position.Y,
		};
		SendEvent("position", data);
		_lastSentPos = position;
	}

	// ═════════════════════════════════════════════════════════════
	//  POSITION HELPER
	// ═════════════════════════════════════════════════════════════

	private void SendPositionIfMoved()
	{
		var player = Core.GameManager.Instance?.GetNodeOrNull<Node2D>("../Player");
		if (player == null)
		{
			// Try finding player in scene tree
			var scene = GetTree().CurrentScene;
			player = scene?.FindChild("Player", true, false) as Node2D;
		}
		if (player == null) return;

		var pos = player.GlobalPosition;
		if (pos.DistanceTo(_lastSentPos) > 2f) // Only send if moved >2px
		{
			SendPosition(pos);
		}
	}

	// ═════════════════════════════════════════════════════════════
	//  ENGINE.IO / SOCKET.IO PROTOCOL
	// ═════════════════════════════════════════════════════════════

	private void HandleMessage(string raw)
	{
		if (string.IsNullOrEmpty(raw)) return;

		char eioType = raw[0];

		switch (eioType)
		{
			case '0': // Engine.IO open
				HandleEioOpen(raw[1..]);
				break;
			case '2': // Engine.IO ping
				_ws.SendText("3"); // pong
				break;
			case '3': // Engine.IO pong
				break;
			case '4': // Engine.IO message (Socket.IO packet)
				HandleSioPacket(raw[1..]);
				break;
		}
	}

	private void HandleEioOpen(string data)
	{
		try
		{
			using var doc = JsonDocument.Parse(data);
			_sid = doc.RootElement.GetProperty("sid").GetString();
			if (doc.RootElement.TryGetProperty("pingInterval", out var pi))
				_pingInterval = pi.GetDouble() / 1000.0;
			GD.Print($"[GameSocket] Engine.IO open, sid={_sid}");
		}
		catch { }

		// Send Socket.IO connect with auth
		string authJson = JsonSerializer.Serialize(new { token = _authToken });
		_ws.SendText($"40{authJson}");
	}

	private void HandleSioPacket(string data)
	{
		if (string.IsNullOrEmpty(data)) return;

		char sioType = data[0];
		string payload = data.Length > 1 ? data[1..] : "";

		switch (sioType)
		{
			case '0': // Socket.IO connect ACK
				_sioConnected = true;
				EmitSignal(SignalName.Connected);
				GD.Print("[GameSocket] Socket.IO connected.");
				break;
			case '2': // Socket.IO event
				HandleSioEvent(payload);
				break;
			case '4': // Socket.IO error
				GD.PrintErr($"[GameSocket] Socket.IO error: {payload}");
				break;
		}
	}

	private void HandleSioEvent(string payload)
	{
		// Socket.IO events are JSON arrays: ["eventName", {data}]
		try
		{
			using var doc = JsonDocument.Parse(payload);
			var arr = doc.RootElement;
			if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() < 2) return;

			string eventName = arr[0].GetString();
			var eventData = arr[1];

			switch (eventName)
			{
				case "chat":
					HandleChatEvent(eventData);
					break;
				case "chat_error":
					string error = eventData.TryGetProperty("error", out var e) ? e.GetString() : "Unknown error";
					EmitSignal(SignalName.ChatErrorReceived, error);
					break;
				case "player_joined":
					HandlePlayerJoined(eventData);
					break;
				case "player_moved":
					HandlePlayerMoved(eventData);
					break;
				case "player_left":
					string leftId = eventData.GetProperty("id").GetString();
					EmitSignal(SignalName.PlayerLeft, leftId);
					break;
			}
		}
		catch (Exception ex)
		{
			GD.PrintErr($"[GameSocket] Event parse error: {ex.Message} | {payload}");
		}
	}

	private void HandleChatEvent(JsonElement data)
	{
		string sender = data.TryGetProperty("sender", out var s) ? s.GetString() : "";
		string senderId = data.TryGetProperty("sender_id", out var si) ? si.GetString() : "";
		string text = data.TryGetProperty("text", out var t) ? t.GetString() : "";
		string type = data.TryGetProperty("type", out var ty) ? ty.GetString() : "say";
		string color = data.TryGetProperty("color", out var c) ? c.GetString() : "";

		EmitSignal(SignalName.ChatReceived, sender, senderId, text, type, color);
	}

	private void HandlePlayerJoined(JsonElement data)
	{
		string id = data.GetProperty("id").GetString();
		string name = data.TryGetProperty("name", out var n) ? n.GetString() : "?";
		string rank = data.TryGetProperty("rank", out var r) ? r.GetString() : "Aspirant";
		string allegiance = data.TryGetProperty("allegiance", out var a) ? a.GetString() : "None";
		float x = data.TryGetProperty("x", out var xv) ? xv.GetSingle() : 0;
		float y = data.TryGetProperty("y", out var yv) ? yv.GetSingle() : 0;

		EmitSignal(SignalName.PlayerJoined, id, name, rank, allegiance, x, y);
	}

	private void HandlePlayerMoved(JsonElement data)
	{
		string id = data.GetProperty("id").GetString();
		float x = data.GetProperty("x").GetSingle();
		float y = data.GetProperty("y").GetSingle();

		EmitSignal(SignalName.PlayerMoved, id, x, y);
	}

	// ═════════════════════════════════════════════════════════════
	//  SEND HELPER
	// ═════════════════════════════════════════════════════════════

	private void SendEvent(string eventName, object data)
	{
		if (_ws == null || !_sioConnected) return;

		string json = JsonSerializer.Serialize(data);
		// Socket.IO event: 42["eventName", {data}]
		_ws.SendText($"42[\"{eventName}\",{json}]");
	}
}
