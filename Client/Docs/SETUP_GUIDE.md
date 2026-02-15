# Project Tactics — Godot 4.x C# Setup Guide

## Prerequisites

1. **Godot 4.3+** with **.NET / C#** support
   - Download from: https://godotengine.org/download
   - Make sure you grab the **.NET** version (not standard)
2. **.NET 8 SDK** installed
   - Download from: https://dotnet.microsoft.com/download
3. **IDE** — VS Code with C# extension, Rider, or Visual Studio

---

## Step 1: Create the Godot Project

1. Open Godot → **New Project**
2. Name: `ProjectTactics`
3. Renderer: **Forward+** (or **Compatibility** if targeting lower-end PCs)
4. Create the project

---

## Step 2: Set Up Folder Structure

In the Godot FileSystem panel, create these folders:

```
res://
├── Scripts/
│   ├── Core/         ← Copy GameManager.cs, PlayerData.cs, RaceData.cs here
│   ├── Player/       ← Copy PlayerController.cs, CameraController.cs here
│   ├── Systems/      ← Copy DailyTraining.cs here
│   ├── UI/           ← Copy DebugOverlay.cs here
│   ├── Combat/       ← (empty for now)
│   ├── RP/           ← (empty for now)
│   ├── Networking/   ← (empty for now)
│   └── Admin/        ← (empty for now)
├── Scenes/
│   ├── Main/
│   ├── Login/
│   ├── World/
│   ├── UI/
│   └── Combat/
├── Assets/
│   ├── Sprites/
│   ├── Tilesets/
│   ├── UI/
│   └── Audio/
└── Docs/
```

Copy all the `.cs` files from the delivered package into their matching folders.

---

## Step 3: Build the C# Solution

Before anything works, Godot needs to compile the C# project:

1. Go to **Project → Tools → C# → Create C# Solution** (if not already created)
2. Click **Build** (the hammer icon in the top right, or MSBuild from terminal)
3. Wait for compilation — check the Output panel for errors
4. If you see namespace errors, make sure all files are in the correct folders

---

## Step 4: Set Up the Autoload (GameManager Singleton)

1. Go to **Project → Project Settings → Autoload**
2. Click the folder icon and select `res://Scripts/Core/GameManager.cs`
3. Set the **Name** to: `GameManager`
4. Make sure **Enable** is checked
5. Click **Add**

This makes `GameManager.Instance` available from any script in the project.

---

## Step 5: Set Up Input Actions

Go to **Project → Project Settings → Input Map** and add these actions:

| Action Name   | Key Binding(s)              |
|---------------|------------------------------|
| `move_up`     | W, Up Arrow                  |
| `move_down`   | S, Down Arrow                |
| `move_left`   | A, Left Arrow                |
| `move_right`  | D, Right Arrow               |
| `run`         | Shift                        |
| `interact`    | E, Enter                     |

### How to add each one:
1. Type the action name in the **"Add New Action"** field at the top
2. Click **Add**
3. Click the **+** button next to the action
4. Press the key you want to bind
5. Click **OK**

---

## Step 6: Create the Test Scene (Overworld)

This gets you a playable character moving around immediately.

### 6a: Create the Scene

1. **Scene → New Scene**
2. Choose **Node2D** as root
3. Rename root to `Overworld`
4. Save as `res://Scenes/World/Overworld.tscn`

### 6b: Add a TileMap (ground)

1. Add child → **TileMapLayer**
2. In the Inspector, create a new **TileSet**
3. For quick testing: add a simple colored square as a tile
   - Or import a tileset image into `Assets/Tilesets/`
4. Paint a floor area (at least 20x20 tiles) so the player has ground

### 6c: Create the Player

1. Add child to Overworld → **CharacterBody2D**
2. Rename to `Player`
3. Attach script: `res://Scripts/Player/PlayerController.cs`
4. Add children to Player:
   - **AnimatedSprite2D** — rename to `Sprite2D`
     - For testing: create a new SpriteFrames, add a simple colored rectangle
     - Or just use a **Sprite2D** with a placeholder texture for now
   - **CollisionShape2D**
     - Shape: RectangleShape2D (16x16 or 32x32)
   - **Area2D** — rename to `InteractionArea`
     - Add **CollisionShape2D** child to it (slightly larger, like 20x20)

The Player node tree should look like:
```
Player (CharacterBody2D) [PlayerController.cs]
├── Sprite2D (AnimatedSprite2D)
├── CollisionShape2D
└── InteractionArea (Area2D)
    └── CollisionShape2D
```

### 6d: Add the Camera

1. Add child to Overworld (NOT to Player) → **Camera2D**
2. Rename to `Camera`
3. Attach script: `res://Scripts/Player/CameraController.cs`
4. In the Inspector:
   - Set **Target Path** to `../Player`
   - Zoom: leave default or set to (2, 2)

### 6e: Add the Debug Overlay

1. Add child to Overworld → **CanvasLayer**
2. Rename to `DebugLayer`
3. Add child to DebugLayer → **Control**
4. Rename to `DebugOverlay`
5. Attach script: `res://Scripts/UI/DebugOverlay.cs`
6. In the Inspector for the Control:
   - Set **Layout** → **Full Rect** (so it covers the screen)
   - Set **Mouse Filter** → **Ignore** (so clicks pass through)

### Final Scene Tree:
```
Overworld (Node2D)
├── TileMapLayer
├── Player (CharacterBody2D) [PlayerController.cs]
│   ├── Sprite2D (AnimatedSprite2D)
│   ├── CollisionShape2D
│   └── InteractionArea (Area2D)
│       └── CollisionShape2D
├── Camera (Camera2D) [CameraController.cs]
└── DebugLayer (CanvasLayer)
    └── DebugOverlay (Control) [DebugOverlay.cs]
```

---

## Step 7: Quick-Start Test Character

The GameManager won't have a login screen yet, so we need to auto-create a test character.
Create this temporary script:

1. Create file `res://Scripts/Core/TestBootstrap.cs`
2. Paste this code:

```csharp
using Godot;
using ProjectTactics.Core;

/// <summary>
/// Temporary bootstrap for testing. Creates a test character on startup.
/// Remove this once the login/character creation screens are built.
/// </summary>
public partial class TestBootstrap : Node
{
    public override void _Ready()
    {
        // Wait one frame for GameManager to initialize
        CallDeferred(nameof(Initialize));
    }

    private void Initialize()
    {
        var gm = GameManager.Instance;
        if (gm == null)
        {
            GD.PrintErr("[TestBootstrap] GameManager not found! Check Autoload.");
            return;
        }

        // Try loading existing save first
        if (gm.SaveExists())
        {
            var loaded = gm.LoadCharacterFromFile();
            if (loaded != null)
            {
                gm.LoadCharacter(loaded);
                GD.Print("[TestBootstrap] Loaded existing character.");
                return;
            }
        }

        // Create a fresh test character
        var data = new PlayerData
        {
            CharacterName = "Test Character",
            RaceName = "Human",
            City = "Lumere"
        };

        // Give some starting stats for testing
        data.Strength = 10;
        data.Speed = 12;
        data.Agility = 11;
        data.Endurance = 8;
        data.Stamina = 9;
        data.EtherControl = 7;
        data.DailyPointsRemaining = 5;

        // Apply race passives and refresh
        RaceData.ApplyRacePassives(data);
        data.InitializeCombatState();

        gm.LoadCharacter(data, "test");
        gm.SetState(GameManager.GameState.InWorld);

        GD.Print("[TestBootstrap] Created test character.");
    }
}
```

3. Add a **Node** to your Overworld scene, rename to `TestBootstrap`
4. Attach `TestBootstrap.cs` to it

Updated scene tree:
```
Overworld (Node2D)
├── TestBootstrap (Node) [TestBootstrap.cs]
├── TileMapLayer
├── Player (CharacterBody2D) [PlayerController.cs]
│   ├── Sprite2D (AnimatedSprite2D)
│   ├── CollisionShape2D
│   └── InteractionArea (Area2D)
│       └── CollisionShape2D
├── Camera (Camera2D) [CameraController.cs]
└── DebugLayer (CanvasLayer)
    └── DebugOverlay (Control) [DebugOverlay.cs]
```

---

## Step 8: Set Main Scene & Run

1. Go to **Project → Project Settings → General → Run**
2. Set **Main Scene** to `res://Scenes/World/Overworld.tscn`
3. Press **F5** (or the Play button)

### What You Should See:
- A player character (placeholder sprite) on a tiled ground
- WASD/Arrow keys to move, Shift to run
- Green debug text in top-left showing all stats
- F1 toggles debug overlay
- F2 allocates a training point (cycles through stats)
- F3 saves the character
- F4 loads the character

---

## Step 9: Verify Everything Works

### Test Checklist:

- [ ] Character moves with WASD
- [ ] Shift makes character run faster
- [ ] Debug overlay shows stats (F1 to toggle)
- [ ] F2 adds stat points and numbers update on screen
- [ ] Derived stats (HP, ATK, etc.) change when training stats change
- [ ] F3 saves without errors in Output
- [ ] F4 loads and restores saved stats
- [ ] Race passives are applied (check derived stat multipliers)
- [ ] Camera follows the player smoothly
- [ ] Mouse wheel zooms in/out

---

## What's Next (Phase 2)

Once this is working, the next steps are:

1. **Login / Character Creation Screen** — Replace TestBootstrap
2. **Flask Backend** — API for auth, character CRUD, persistent database
3. **Daily Training UI** — Proper allocation screen (not debug keys)
4. **Chat System** — Say, Whisper, OOC, Emote verbs
5. **Multiplayer** — Godot networking for seeing other players
6. **Map Design** — Real tilesets, city layouts

---

## Troubleshooting

### "GameManager not found"
- Make sure it's added as an Autoload (Step 4)
- Make sure you've built the C# solution (Step 3)

### "Namespace not found" errors
- Build the solution: Project → Tools → C# → Build
- Check that filenames match class names

### Character doesn't move
- Check Input Map actions match exactly: `move_up`, `move_down`, etc.
- Make sure PlayerController.cs is attached to the CharacterBody2D

### Debug overlay is empty
- GameManager.Instance.CurrentPlayer is null → TestBootstrap didn't run
- Check Output panel for errors

### Save/Load doesn't work
- Check Output panel for path errors
- Saves go to `user://saves/` which is:
  - Windows: `%AppData%/Godot/app_userdata/ProjectTactics/saves/`
  - Mac: `~/Library/Application Support/Godot/app_userdata/ProjectTactics/saves/`
  - Linux: `~/.local/share/godot/app_userdata/ProjectTactics/saves/`
