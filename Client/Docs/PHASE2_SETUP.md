# Phase 2 Setup Guide - Login & Character Creation Flow

## Files to Copy

Copy these files into your Godot project, **replacing** the old versions:

```
Scripts/Core/GameManager.cs    ← UPDATED (new scene paths, PendingSlot, DeleteSave)
Scripts/Core/PlayerData.cs     ← UPDATED (added Bio, PlayByPath fields)
Scripts/UI/TitleScreen.cs      ← NEW
Scripts/UI/CharacterSelect.cs  ← NEW
Scripts/UI/CharacterCreate.cs  ← NEW
```

## Step 1: Copy the Files

Drop the updated files into your project's Scripts/ folders:
- `GameManager.cs` and `PlayerData.cs` go into `Scripts/Core/`
- `TitleScreen.cs`, `CharacterSelect.cs`, `CharacterCreate.cs` go into `Scripts/UI/`

## Step 2: Create the Three Scenes

### Scene A: TitleScreen.tscn

1. **Scene → New Scene**
2. Choose **User Interface** (Control root)
3. Rename root to `TitleScreen`
4. Right-click `TitleScreen` → **Attach Script** → select `res://Scripts/UI/TitleScreen.cs`
5. Save as `res://Scenes/Login/TitleScreen.tscn`

That's it — the UI builds itself in code. No child nodes needed.

### Scene B: CharacterSelect.tscn

1. **Scene → New Scene**
2. Choose **User Interface** (Control root)
3. Rename root to `CharacterSelect`
4. Right-click → **Attach Script** → select `res://Scripts/UI/CharacterSelect.cs`
5. Save as `res://Scenes/Login/CharacterSelect.tscn`

### Scene C: CharacterCreate.tscn

1. **Scene → New Scene**
2. Choose **User Interface** (Control root)
3. Rename root to `CharacterCreate`
4. Right-click → **Attach Script** → select `res://Scripts/UI/CharacterCreate.cs`
5. Save as `res://Scenes/Login/CharacterCreate.tscn`

## Step 3: Set Main Scene to TitleScreen

1. **Project → Project Settings → General → Application → Run**
2. Set **Main Scene** to `res://Scenes/Login/TitleScreen.tscn`
3. Close settings

## Step 4: Build and Run

1. Click the **MSBuild** tab at the bottom → **Build**
2. Fix any errors if they appear
3. Press **F5** to run

## Expected Flow

```
F5 → Title Screen
       │
       ├── "ENTER GAME" button
       │       ↓
       │   Character Select Screen
       │       │
       │       ├── Slot 1: [Empty] → "CREATE NEW" → Character Creation Wizard
       │       ├── Slot 2: [Empty] → "CREATE NEW" → Character Creation Wizard
       │       ├── Slot 3: [Empty] → "CREATE NEW" → Character Creation Wizard
       │       │
       │       └── [After creating] → Slot shows character → "PLAY" → Overworld
       │
       └── "QUIT" button → Closes game
```

## Character Creation Flow

```
Step 1: Enter Name (2-30 characters)
    ↓
Step 2: Choose Village
    - Konohagakure (The Village Hidden in the Leaves)
    - Sunagakure (The Village Hidden in the Sand)
    - Kirigakure (The Village Hidden in the Mist)
    ↓
Step 3: Write Bio (optional, 500 char max) + Upload Play-By Image (optional)
    ↓
CREATE → Auto-saves to slot → Loads into Overworld
```

## Notes

- All UI is built dynamically in code — no need to add child nodes manually
- The scenes are just a single Control root with the script attached
- Orange (#E87722) accent color theme throughout
- Dark background (#1a1a2e) for the Naruto aesthetic
- Saves go to `user://saves/slot1.tres`, `slot2.tres`, `slot3.tres`
- You can delete your old TestBootstrap node from the Overworld scene
  since characters now enter through the proper creation flow
- The Overworld scene still needs the Player, Camera, TileMap, and
  DebugOverlay nodes — those haven't changed
