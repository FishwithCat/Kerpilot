# Kerpilot

A Kerbal Space Program mod that provides an in-game AI chat assistant powered by any OpenAI-compatible LLM API.

## Features

- Modern dark-themed chat interface with rounded message bubbles
- LLM integration with streaming responses (token-by-token display)
- **Game-aware tools** — the AI can query live game data via function calling:
  - Vessel part composition (names, counts, masses, resources)
  - Vessel delta-v budget per stage (delta-v, TWR, ISP, burn time)
  - Vessel orbit parameters (Ap/Pe, inclination, eccentricity, period)
  - Vessel flight status (altitude, speed, G-force, electric charge, CommNet)
  - Part details (description, cost, mass, category, resource capacities)
  - Celestial body parameters (gravity, atmosphere, SOI, orbital data)
  - Atmosphere profiles (pressure, temperature, density at multiple altitudes)
  - Active contracts (objectives, rewards, completion state)
  - KSP Wiki search (tutorials, game mechanics, guides)
- Settings panel to configure API endpoint, API key, and model
- Supports any OpenAI-compatible API (OpenAI, Anthropic via proxy, local models, etc.)
- Toolbar button and `Ctrl+K` keyboard shortcut to toggle the window
- Draggable window
- Input lock prevents chat keystrokes from triggering vessel controls
- Available in Space Center, Flight, and Map View scenes (vessel/contract tools in Flight only, wiki always available)

## Requirements

- KSP 1.12.5
- [.NET SDK](https://dotnet.microsoft.com/download) (6.0+)
- [Mono](https://www.mono-project.com/download/stable/) (for net472 reference assemblies)

## Build

1. Clone the repository:

   ```bash
   git clone https://github.com/your-username/Kerpilot.git
   cd Kerpilot
   ```

2. Verify the KSP path in `Kerpilot.csproj` matches your installation. The default is:

   ```
   /Users/linear/Library/Application Support/Steam/steamapps/common/Kerbal Space Program
   ```

   Update the `<KSPRoot>` property if your KSP is installed elsewhere.

3. Build:

   ```bash
   dotnet build -c Release
   ```

   The compiled DLL is automatically copied to `GameData/Kerpilot/Plugins/`.

## Install

Symlink or copy the `GameData/Kerpilot` folder into your KSP `GameData` directory:

```bash
ln -s "$(pwd)/GameData/Kerpilot" "/path/to/Kerbal Space Program/GameData/Kerpilot"
```

## Usage

1. Launch KSP and enter Space Center or Flight
2. Click the **K** toolbar button or press **Ctrl+K** to open the chat window
3. Click the **gear icon** (⚙) in the header to open Settings
4. Enter your API endpoint (default: `https://api.openai.com/v1`), API key, and model name
5. Click **Save**, then **Back** to return to chat
6. Type a message and press **Send** or **Enter** — the AI will respond with streamed tokens

## License

[GPL-3.0](LICENSE)
