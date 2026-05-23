# AetherShatter — GoDGuilD Edition

A neon-synthwave Breakout/Arkanoid clone built with **MonoGame (DesktopGL)**.  
Developed by **GoDGuilD StudioS**.

---

## Prerequisites

| Tool | Version | Download |
|------|---------|----------|
| .NET SDK | 8.0+ | https://dotnet.microsoft.com/download |
| MonoGame templates | 3.8.1+ | `dotnet new install MonoGame.Templates.CSharp` |
| MonoGame Content Builder (mgcb) | 3.8.1+ | `dotnet tool install -g dotnet-mgcb-editor` |

---

## First-Time Setup

```powershell
# 1. Install the MonoGame project templates (one-time)
dotnet new install MonoGame.Templates.CSharp

# 2. Restore NuGet packages
cd AetherShatter
dotnet restore

# 3. Build the Content (compiles the SpriteFont)
#    If mgcb-editor is installed, open Content/Content.mgcb and Build.
#    Or via CLI:
dotnet tool install -g dotnet-mgcb
mgcb Content/Content.mgcb /platform:DesktopGL

# 4. Run the game
dotnet run
```

> **No font?**  The game degrades gracefully — all shapes still render; only
> text labels are hidden.  Build the Content pipeline at least once for the
> full experience.

---

## Controls

| Action | Input |
|--------|-------|
| Move paddle | Mouse (preferred) / Arrow Keys / A-D |
| Launch ball | Left Click / Space |
| Pause / Back | Escape |

---

## Architecture

```
AetherShatter/
├── AetherShatterGame.cs     # MonoGame entry point, shared resources
├── Program.cs
├── Core/
│   ├── GameStateManager.cs  # Push-style FSM
│   ├── InputManager.cs      # Mouse+KB now; swap in touch for Android
│   └── ParticleSystem.cs    # Pre-allocated pool (zero GC in game loop)
├── Entities/
│   ├── GameObject.cs        # Base class
│   ├── Paddle.cs
│   ├── Ball.cs              # AABB collision + paddle deflection
│   ├── Brick.cs             # Tiered HP, glow rendering
│   └── PowerUp.cs           # Multiball, PaddleExpand
├── States/
│   ├── SplashState.cs       # Fade-in/out "GoDGuilD StudioS Presents"
│   ├── MenuState.cs
│   ├── GameplayState.cs     # Core game loop, screen shake
│   ├── GameOverState.cs
│   └── VictoryState.cs      # Particle fireworks
├── Levels/
│   └── LevelData.cs         # 3 hardcoded int[,] layouts
└── Content/
    ├── Content.mgcb
    └── Font.spritefont      # Arial 14pt
```

### Android Migration Checklist
1. Change `<PackageReference>` from `MonoGame.Framework.DesktopGL` → `MonoGame.Framework.Android`.
2. Replace `Program.cs` with an Android Activity entry point.
3. In `InputManager`, call `SetTouchInput(normX, tap)` from the touch event handler instead of `Update()`.
4. Adjust `_graphics.PreferredBackBufferWidth/Height` to match device screen.

---

## Levels

| Level | Theme |
|-------|-------|
| 1 | Classic symmetric grid — Glass and Neon mix |
| 2 | Diamond of Core bricks surrounded by Neon rings |
| 3 | Gauntlet — heavy Core wall with narrow escape gaps |

---

## Power-Ups

| Icon colour | Type | Effect |
|-------------|------|--------|
| Green | Multiball | Spawns 2 extra balls |
| Orange | Paddle Expand | Widens paddle 50% for 10 s |

---

*© 2024 GoDGuilD StudioS — All rights reserved.*
