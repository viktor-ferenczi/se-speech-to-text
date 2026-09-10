# Speech recognition support for Space Engineers 1 

Connects the [Handy](https://github.com/cjpais/Handy) speech to text application to the game, plugins, mods and scripts.

## Prerequisites

- [Space Engineers](https://store.steampowered.com/app/244850/Space_Engineers/)
- [Pulsar](https://github.com/SpaceGT/Pulsar)
- [.NET Framework 4.8.1 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net481) and
  [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Handy](https://github.com/cjpais/Handy)

## Features

- External paste script to configure with Handy (separate for Linux and Windows)
- Plugin, mod and script API to receive events with text transcribed from speech input.

## How to use 

1. Enable this plugin in Pulsar's Plugins dialog.
2. Configure Handy and test it in a text editor: https://github.com/cjpais/Handy
3. In Handy's Settings, under Advanced / Output change the Paste Method to External Script,
   then copy the path to the script from the plugin's configuration.
4. By default, the plugin is configured to open the chat panel and paste the text into the latest chat.
   This can be disabled in the configuration. 
   
### Support

- In case of problems create a new thread in the `#support` channel of the
  [Pulsar Discord](https://discord.gg/z8ZczP2YZY)
