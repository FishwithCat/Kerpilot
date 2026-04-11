# Kerpilot

A Kerbal Space Program mod that provides a modern chat dialog built with Unity uGUI. Designed as a foundation for future LLM integration.

## Features

- Modern dark-themed chat interface with rounded message bubbles
- Toolbar button and `Ctrl+K` keyboard shortcut to toggle the window
- Draggable window
- Input lock prevents chat keystrokes from triggering vessel controls
- Available in Space Center, Flight, and Map View scenes

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
3. Type a message and press **Send** or **Enter**
4. The current MVP replies with "Thinking..." to all messages

## License

[GPL-3.0](LICENSE)
