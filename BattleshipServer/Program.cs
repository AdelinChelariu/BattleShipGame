using BattleshipServer;

Console.Title = "Battleship Server";
Console.WriteLine("╔════════════════════════════════╗");
Console.WriteLine("║     BATTLESHIP SERVER v1.0     ║");
Console.WriteLine("╚════════════════════════════════╝");
Console.WriteLine();

// Port implicit 5000, sau din argumentul din linie de comandă
int port = 5000;
if (args.Length > 0 && int.TryParse(args[0], out int customPort))
    port = customPort;

var server = new Server(port);

// Oprire curată cu Ctrl+C
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.WriteLine("\n[Server] Oprire...");
    server.Stop();
};

server.Start();
