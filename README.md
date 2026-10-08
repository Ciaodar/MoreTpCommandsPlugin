# Subnautica: Below Zero Multiplayer — More TP Commands Plugin

A lightweight, feature-rich quality-of-life plugin for the **[Subnautica: Below Zero Multiplayer Mod](https://github.com/ismail0234/Subnautica-Below-Zero-Multiplayer)** that adds player-to-player teleportation overloads (`warp`), a return-to-death-point command (`back`), and full `Up` / `Down` arrow command history navigation in the multiplayer chat box.

---

## ✨ Features

### 1. Overloaded `warp` Command (Player-to-Player Teleport)
* **Teleport to a Player (`warp <targetPlayer>`):** Instantly teleports you to the target player's current position.
* **Teleport Another Player (`warp <player> <targetPlayer>`):** Teleports `<player>` to `<targetPlayer>`. Works across clients via network synchronization—for example, summon a friend to your location with `warp FriendName me`.
* **Smart Name Matching:** Case-insensitive and supports partial nicknames (e.g., `warp den` matches `Deniz`) as well as keywords like `me`, `self`, or `myself`.
* **Vanilla Coordinate Compatibility:** Standard `warp <x> <y> <z>` coordinate commands continue to work natively without interference.

### 2. Return to Last Death Location (`back`)
* Automatically records your exact coordinates whenever you die (`Player.OnKill`).
* Persists your last death location to disk (`MoreTpCommands_Death.json`) so you can still return even after reconnecting or restarting the game.
* Type `back` or `/back` in the multiplayer chat box or developer console to teleport right back to where you died.

### 3. Command History Navigation (`Up` / `Down` Arrows)
* Brings full terminal-style command history to the multiplayer **ChatBox** (`Enter`) and synchronizes seamlessly with the **DevConsole**.
* Press **Up Arrow (`↑`)** to recall previously executed commands and **Down Arrow (`↓`)** to navigate forward.
* Preserves your unsubmitted draft text at the bottom of the history stack and keeps the text cursor positioned at the end of the input field for quick editing.

### 4. Safe Interior / Exterior & Vehicle Teleportation
* **Interior-to-Exterior State Reset:** Cleanly exits bases (`SubRoot`), Moonpools (`BaseEntranceTrigger` / `PrecursorMoonPoolTrigger`), Alien Sanctuaries (`forceWalkMotorMode`), WaterParks, and multiplayer SeaTruck interiors (`SeaTruckCustomMotor`) before teleporting, recalculating `PlayerController` and `UnderwaterMotor` / `GroundMotor` states so your character never gets stuck in walking physics underwater.
* **Vehicle Unmounting:** Automatically exits SeaTruck piloting chairs, Prawn Suits (`Exosuit`), Snowfox (`Hoverbike`), and sitting/locked animations before warping.
* **Instant Coordinate Warp:** Upgrades `warp <x> <y> <z>` to use instant safe teleportation instead of vanilla's slow `GotoPosition` translation that could leave interior states dirty.

---

## 🎮 Commands & Usage

You can enter commands either in the **Multiplayer ChatBox** (with or without a leading `/`) or in the **Developer Console**:

| Command | Description | Example |
| :--- | :--- | :--- |
| `warp <targetPlayer>` | Teleport yourself to `<targetPlayer>`. | `warp Alex` or `/warp Alex` |
| `warp <player> <targetPlayer>` | Teleport `<player>` to `<targetPlayer>`. | `warp Alex Sam` or `/warp Alex me` |
| `warp <x> <y> <z>` | Instant safe coordinate teleport. | `warp 100 -25 340` |
| `warp help` | Display in-game usage instructions. | `warp help` |
| `back` | Teleport to your most recent death location. | `back` or `/back` |

---

## 📦 Installation

1. Download or build `Subnautica.MoreTpCommands.dll`.
2. Copy `Subnautica.MoreTpCommands.dll` into your BOT Benson Multiplayer Mod `Plugins` directory:
   ```text
   %AppData%\.botbenson\Subnautica Below Zero\Game\Plugins\
   ```
3. **Important for Co-op:** Ensure all players in the session have the plugin installed in their `Plugins` folder so remote teleport requests (`warp <player> <targetPlayer>`) can be received and executed by each client.

---

## 🛠️ Building from Source

### Prerequisites
* [.NET SDK](https://dotnet.microsoft.com/download) (targeting `.NET Framework 4.7.2`)
* Installed copy of **Subnautica: Below Zero** and the **BOT Benson Multiplayer Mod**

### Setup Dependencies
Copy the required reference assemblies into the `Libs/` directory at the root of this repository:
* **From Subnautica: Below Zero (`SubnauticaZero_Data/Managed/`):**
  * `Assembly-CSharp.dll`
  * `Assembly-CSharp-firstpass.dll`
  * `UnityEngine.dll`
  * `UnityEngine.CoreModule.dll`
  * `UnityEngine.PhysicsModule.dll`
  * `UnityEngine.UI.dll`
* **From BOT Benson Multiplayer Mod (`%AppData%\.botbenson\Subnautica Below Zero\Game\Dependencies\`):**
  * `0Harmony.dll`
  * `Newtonsoft.Json.dll`
  * `Subnautica.API.dll`
  * `Subnautica.Client.dll`
  * `Subnautica.Events.dll`
  * `Subnautica.Network.dll`
  * `Subnautica.Server.dll`

### Build & Run Tests
```bash
# Build Release DLL (automatically copies to Plugins folder if installed)
dotnet build MoreTpCommandsPlugin.csproj -c Release

# Run the xUnit test suite (40 unit tests)
dotnet test MoreTpCommandsPlugin.Tests/MoreTpCommandsPlugin.Tests.csproj
```

---

## 🙏 Credits & Acknowledgements

Huge thanks to **BOT Benson ([@ismail0234](https://github.com/ismail0234))** for creating, maintaining, and open-sourcing the **[Subnautica: Below Zero Multiplayer Mod](https://github.com/ismail0234/Subnautica-Below-Zero-Multiplayer)**. This plugin is built upon its plugin architecture and event system.

* **Core Multiplayer Mod Repository:** [https://github.com/ismail0234/Subnautica-Below-Zero-Multiplayer](https://github.com/ismail0234/Subnautica-Below-Zero-Multiplayer)
