# Speech to text support for Space Engineers 1

Connects the [Handy](https://github.com/cjpais/Handy) speech to text application to the game, plugins, mods and scripts.

## Prerequisites

- [Space Engineers](https://store.steampowered.com/app/244850/Space_Engineers/)
- [Pulsar](https://github.com/SpaceGT/Pulsar)
- [.NET Framework 4.8.1 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481) and
  [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Handy](https://github.com/cjpais/Handy)

## Features

- Two ways to connect Handy: an external paste script (separate for Linux and Windows),
  or a Webhook URL once a Handy release supports it
- Pastes the transcribed text into the chat (faction, global or the last used channel),
  optionally sending it right away
- Sends the transcribed text to mods and plugins over mod communication channels
- Runs programmable blocks with the transcribed text as their argument
- A hotkey (`Ctrl+Alt+S` by default) opens a selector dialog to switch the individual
  targets on and off while playing

## Setting up Handy

Handy does the speech to text; this plugin only takes the result off it. So get Handy working on
its own first, and only then point it at the game.

1. Install Handy from its [releases page](https://github.com/cjpais/Handy/releases) (on macOS
   `brew install --cask handy`, on Windows `winget install cjpais.Handy`), launch it and grant the
   permissions it asks for: the microphone, and accessibility so it may paste into other
   applications.
2. In Handy's Settings choose a transcription model - a Whisper model (Small/Medium/Turbo/Large,
   GPU accelerated where available) or Parakeet V3, which runs on the CPU and detects the language
   by itself.
3. Set the keyboard shortcut to dictate with. Hold it while speaking and release, or tap it to
   toggle recording, depending on the mode you pick.
4. Test it in a text editor: press the shortcut, say a sentence, release, and the text should
   appear at the caret.

Do not move on until step 4 works. Everything below only changes *where* the text ends up, so a
Handy that does not transcribe into a text editor will not transcribe into the game either.

## How to use 

1. Enable this plugin in Pulsar's Plugins dialog.
2. Set Handy up and test it in a text editor, as above.
3. Connect Handy to the game with one of the two paste methods below.
4. By default the transcript goes to the mod communication channels and to the matching
   programmable blocks, while pasting into the chat is off - so nothing shows up on screen
   until a mod, plugin or script acts on it. Turn `Paste into chat` on in the plugin's
   configuration to get the text into the game's chat instead.
5. While playing press `Ctrl+Alt+S` to switch the configured targets on and off individually.

### Which paste method to use

As of Handy **0.9.6** only `External Script` exists, so that is the one to set up today. `Webhook`
is a pull request waiting to be merged; the first release carrying it makes the simpler method
available. Nothing in this plugin has to change for that - the two speak the same protocol and the
plugin already serves both, so switching over is a matter of changing one setting in Handy.

### Connecting Handy: External script

In Handy's Settings, under Advanced / Output set the Paste Method to `External Script` and paste
the path shown in the plugin's configuration (`Copy script path` puts it on the clipboard).

The path points into Pulsar's own plugin folder, so it is not stable: **moving or reinstalling
Pulsar changes it**, and Handy keeps calling the old one, which silently stops the text from ever
reaching the game. After any such change, copy the path out of the plugin's configuration again
and paste it into Handy.

Unlike the webhook, the script performs the paste itself when the game does not take the text,
so it needs the usual clipboard tools on the host (`xclip`/`xdotool`, or `wl-copy` with
`ydotool`/`wtype` on Wayland). On Windows, Handy hides `External Script` in its UI, so the paste
method has to be set by hand with Handy closed, in `%APPDATA%\com.pais.handy\settings_store.json`:

```json
"paste_method": "external_script",
"external_script_path": "<path from the plugin's config dialog>"
```

### Connecting Handy: Webhook

Once a Handy release offers it: in Handy's Settings, under Advanced / Output set the Paste Method
to `Webhook` and paste the URL shown in the plugin's configuration (`Copy webhook URL` puts it on
the clipboard). It follows the configured port, so `http://127.0.0.1:5115/handy` by default.

Handy posts the transcript to that URL and pastes it itself only if the game does not answer
`{"handled":true}`, exactly as the script does. Prefer it once it is available: nothing has to be
installed or made executable, it works the same on Linux and Windows, and the URL does not go
stale when Pulsar moves.

Handy gives the endpoint 400 ms to answer by default. Keep the plugin's `Timeout (ms)` below that,
otherwise Handy gives up and pastes the text while the game is still handling it.

## When the plugin takes the text

The plugin takes the text only while the game window is focused and you are in the world:
the gameplay screen, the chat, or the terminal. In the terminal it steps aside as soon as one of
its textboxes has the keyboard (search, block name, Custom Data, a script's run argument), and in
any other menu it never takes the text, so Handy pastes into the focused textbox as usual and
dictation keeps working there.

Watching a script's output in the terminal therefore works: the block's Detailed Info repaints only
while the terminal is open, so the transcript has to arrive while you are looking at it.

## For mod, plugin and script authors

The transcribed text arrives with the configured message prefix (`STT:` by default), so the message
`STT:raise the landing gear` means the player said "raise the landing gear". Mods, plugins and
programmable blocks all receive exactly the same string.

- Mods and plugins: `MyAPIGateway.Utilities.RegisterMessageHandler(channel, handler)` receives it as
  a `string` on each configured channel (default: `511505115`).
- Programmable blocks: every block with terminal access whose name matches one of the configured
  patterns is run with the message as its argument. `*` matches anything, so `*[STT]` (the default)
  runs every block whose name ends with `[STT]`. The configuration decides whether only the
  controlled grid and its subgrids are searched, all loaded grids, or the first with the second as
  a fallback.

## How Handy talks to the game

Both paste methods speak the same protocol: a JSON `POST` to `http://127.0.0.1:5115/handy`
carrying the transcript, answered with `{"handled":true}` once the game has taken the text, or
`{"handled":false}` if it has not. Handy pastes the text itself only on the second answer, so with
the game closed, unfocused or the plugin disabled it keeps working as a plain dictation tool.

The plugin listens on the loopback interface only. The port and the time the game may take to
answer (300 ms by default) are configurable. Raising the timeout means raising Handy's own
timeout too, otherwise Handy gives up and pastes while the game handles the text as well:
that limit is Handy's `webhook_timeout_ms` (400 ms by default) for the webhook, and the script's
own request timeout of one second for the script. A port other than the default must also be
given to the script through the `HANDY_HOOK_PORT` environment variable; the webhook URL carries
the port itself.
   
### Support

- In case of problems create a new thread in the `#support` channel of the
  [Pulsar Discord](https://discord.gg/z8ZczP2YZY)
