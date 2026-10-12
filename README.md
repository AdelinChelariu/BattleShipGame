# BattleshipGame

A multiplayer Battleship game built with **C# and .NET 8**. Players connect to a central TCP server, meet in a lobby, challenge each other, place their fleets on a 10×10 grid and take turns attacking. The client is a WPF desktop app, and the server is a console application that can run many games at once.

The project was built for the course *Sisteme de programe pentru rețele de calculatoare* (2025-2026). A detailed technical write-up in Romanian is included in [`Documentatie_BattleshipGame.docx`](Documentatie_BattleshipGame.docx).

## Features

- **Lobby:** see who is online, chat with everyone in the lobby, and challenge a player. The other player can accept or decline.
- **Fleet placement:** place the five classic ships on a 10×10 grid, rotate with the **R** key or the rotate button, or use random placement.
- **Turn-based combat:** hits, misses and sunk ships are shown on both boards, and a sunk ship reveals all of its cells.
- **30-second turn timer:** if a player runs out of time, the server fires a random, not-yet-attacked cell for them.
- **In-game chat** in its own window.
- **Live statistics:** ships remaining, shots fired and hits for both players.
- **Rematch:** both players can agree to play again without going back to the lobby.
- **Disconnect handling:** the opponent is notified if the other player leaves.
- **Concurrent games:** every pair of players runs in an independent game session on the server.

### Fleet

| Ship | Size |
|---|---|
| Portavion (carrier) | 5 |
| Cuirasat (battleship) | 4 |
| Distrugator (destroyer) | 3 |
| Submarin (submarine) | 3 |
| Barca (patrol boat) | 2 |

## Architecture

The solution has three projects:

| Project | Type | Role |
|---|---|---|
| `Shared` | Class library (.NET 8) | Models and protocol shared by both sides: `Protocol`, `Board`, `Ship`, `CellState`, `Direction` |
| `BattleshipServer` | Console app (.NET 8) | TCP server: accepts connections, manages the lobby and runs game sessions |
| `BattleshipClient` | WPF app (.NET 8, Windows) | Connect dialog, lobby window, game window and chat window |

```
          ┌────────────────────────────────┐
          │             Shared             │
          │  Protocol · Board · Ship · ... │
          └───────┬────────────────┬───────┘
                  │                │
        ┌─────────▼────────┐  ┌────▼─────────────┐
        │ BattleshipServer │  │ BattleshipClient │
        │                  │  │                  │
        │  Server          │  │  ConnectDialog   │
        │  LobbyManager    │◄═►  LobbyWindow     │
        │  GameSession     │TCP  MainWindow      │
        │  ClientHandler   │  │  ChatWindow      │
        └──────────────────┘  └──────────────────┘
```

On the server, each connection gets a `ClientHandler` with its own listener thread. The `LobbyManager` handles names, challenges and lobby chat, and creates a `GameSession` for each matched pair. The `GameSession` owns both boards, enforces turn order and runs the turn timer.

### Protocol

Client and server exchange plain-text messages over TCP, one per line, in the form `TYPE arg1 arg2 ...`. The full list with formats is in [`Shared/Protocol.cs`](Shared/Protocol.cs).

| Phase | Messages |
|---|---|
| Lobby | `LOBBY_WELCOME`, `JOIN`, `LOBBY_UPDATE`, `LOBBY_CHAT`, `CHALLENGE`, `CHALLENGED_BY`, `CHALLENGE_ACCEPT`, `CHALLENGE_DECLINE`, `CHALLENGE_DECLINED`, `GAME_READY` |
| Setup | `PLAYER_ID`, `PLAYER_NAMES`, `START_PLACEMENT`, `PLACE`, `PLACE_OK`, `PLACE_ERR`, `GAME_START` |
| Play | `YOUR_TURN`, `WAIT`, `ATTACK`, `HIT`, `MISS`, `SUNK`, `TURN_TIMEOUT`, `CHAT` |
| End | `WIN`, `LOSE`, `REMATCH_REQUEST`, `REMATCH_REQUESTED`, `REMATCH_ACCEPTED`, `OPPONENT_DISCONNECTED` |
| General | `ERROR` |

Example exchange for placing a ship and attacking:

```
Client → Server   PLACE 2 3 H 4      # row 2, column 3, horizontal, size 4
Server → Client   PLACE_OK
Client → Server   ATTACK 4 7
Server → Both     HIT 4 7
```

## Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows for the client (WPF). The server also runs on other platforms supported by .NET 8.

### 1. Start the server

```bash
dotnet run --project BattleshipServer          # listens on port 5000
dotnet run --project BattleshipServer -- 6000  # or choose another port
```

Stop it with `Ctrl+C`.

### 2. Start two clients

Run the client twice, on the same machine or on different ones:

```bash
dotnet run --project BattleshipClient
```

In the connect dialog, enter:

| Field | Default | Notes |
|---|---|---|
| Name | `Jucator1` | No spaces, at most 20 characters, and unique among connected players |
| Server (host) | `127.0.0.1` | Use the server machine's IP address to play over a network |
| Port | `5000` | Must match the server port |

To play over a network, make sure the server's port is open in its firewall.

### Open in Visual Studio or Rider

Open `BattleshipGame.sln`, set `BattleshipServer` and `BattleshipClient` as startup projects, and run. In Visual Studio you can start several instances of the client from *Debug → Start New Instance*.

## How to play

1. Connect, then pick an opponent in the lobby and send a challenge. The other player accepts it.
2. Place all five ships. Press **R** to rotate, or use random placement.
3. When both fleets are placed, the game starts. On your turn, click a cell on the enemy grid. You have 30 seconds per turn.
4. Sink all of the opponent's ships to win. Then choose **Joacă din nou** (play again) to rematch.

## Project structure

```
.
├── BattleshipGame.sln
├── Shared/
│   ├── Protocol.cs            # Message types and build/parse helpers
│   ├── Board.cs               # 10×10 grid, ship placement and attack rules
│   └── Ship.cs                # Ship model
├── BattleshipServer/
│   ├── Program.cs             # Entry point (optional port argument)
│   ├── Server.cs              # TCP listener
│   ├── ClientHandler.cs       # Per-connection reader and sender
│   ├── LobbyManager.cs        # Names, challenges, lobby chat, matchmaking
│   └── GameSession.cs         # Game state, turns, timer, rematch
├── BattleshipClient/
│   ├── Client.cs              # Network layer and events
│   ├── ConnectDialog.xaml     # Name, host and port
│   ├── LobbyWindow.xaml       # Player list, chat, challenges
│   ├── MainWindow.xaml        # Game board, statistics, timer
│   └── ChatWindow.xaml        # In-game chat
└── Documentatie_BattleshipGame.docx   # Technical documentation (Romanian)
```

## Notes

- The protocol is unencrypted plain text with no authentication, so it is intended for learning and local or trusted networks.
- Build output (`bin/`, `obj/`) and IDE files (`.idea/`, `.claude/`) are currently tracked in the repository. Adding a `.gitignore` for them would keep the repo clean and much smaller.
