using System;
using System.Collections.Generic;

namespace PkillGame.Terminal
{
    public class CommandResult
    {
        public string[] Lines;
        public bool     IsError;

        public static CommandResult Ok(params string[] lines)    => new() { Lines = lines, IsError = false };
        public static CommandResult Error(params string[] lines) => new() { Lines = lines, IsError = true  };
    }

    public class CommandRegistry
    {
        // ── Game logic delegates ──────────────────────────────────────────────────
        // Wire these up from your game scene scripts before the terminal is used.
        public Func<string, CommandResult>             OnSpawn;
        public Func<CommandResult>                     OnDel;
        public Func<string, CommandResult>             OnGive;
        public Func<float, float, float, CommandResult> OnTeleport;
        public Func<string, CommandResult>             OnSay;

        // ─────────────────────────────────────────────────────────────────────────

        class CommandDef
        {
            public string Name;
            public string Usage;
            public string Desc;
            public Func<string[], CommandResult> Handler;
            public CommandDef(string name, string usage, string desc, Func<string[], CommandResult> h)
            { Name = name; Usage = usage; Desc = desc; Handler = h; }
        }

        readonly Dictionary<string, CommandDef> m_Commands = new();

        public CommandRegistry()
        {
            Reg("help",  "help [command]",   "show this help or describe a command", DoHelp);
            Reg("spawn", "spawn <prefab>",   "spawn a prop at your crosshair",       DoSpawn);
            Reg("del",   "del",              "delete the prop you are aiming at",    DoDel);
            Reg("give",  "give <item>",      "give yourself a weapon or item",       DoGive);
            Reg("tp",    "tp <x> <y> <z>",  "teleport to world coordinates",        DoTp);
            Reg("say",   "say <message>",    "send a chat message",                  DoSay);
        }

        void Reg(string name, string usage, string desc, Func<string[], CommandResult> handler)
            => m_Commands[name] = new CommandDef(name, usage, desc, handler);

        public CommandResult Execute(string command, string[] args)
        {
            if (!m_Commands.TryGetValue(command.ToLower(), out var def))
                return CommandResult.Error(
                    $"bash: {command}: command not found",
                    "type 'help' for a list of commands");

            return def.Handler(args);
        }

        // Returns the first command that starts with the input (Tab autocomplete).
        // Case-insensitive. Returns null if no extension is possible.
        public string Autocomplete(string input)
        {
            var lower = input.ToLower();
            foreach (var key in m_Commands.Keys)
                if (key.StartsWith(lower) && key != lower) return key;
            return null;
        }

        // ── Command handlers ─────────────────────────────────────────────────────

        CommandResult DoHelp(string[] args)
        {
            if (args.Length > 0)
            {
                if (m_Commands.TryGetValue(args[0].ToLower(), out var def))
                    return CommandResult.Ok($"  usage: {def.Usage}", $"  {def.Desc}");
                return CommandResult.Error($"help: no manual entry for {args[0]}");
            }

            var lines = new List<string> { "available commands:" };
            foreach (var def in m_Commands.Values)
                lines.Add($"  {def.Usage,-24}{def.Desc}");
            return CommandResult.Ok(lines.ToArray());
        }

        CommandResult DoSpawn(string[] args)
        {
            if (args.Length < 1)
                return CommandResult.Error("usage: spawn <prefab>", "type 'help' for a list of commands");
            return OnSpawn?.Invoke(args[0])
                ?? CommandResult.Error("spawn: no game context — wire up CommandRegistry.OnSpawn");
        }

        CommandResult DoDel(string[] args)
        {
            return OnDel?.Invoke()
                ?? CommandResult.Error("del: no game context — wire up CommandRegistry.OnDel");
        }

        CommandResult DoGive(string[] args)
        {
            if (args.Length < 1)
                return CommandResult.Error("usage: give <item>", "type 'help' for a list of commands");
            return OnGive?.Invoke(args[0])
                ?? CommandResult.Error("give: no game context — wire up CommandRegistry.OnGive");
        }

        CommandResult DoTp(string[] args)
        {
            if (args.Length < 3
                || !float.TryParse(args[0], out float x)
                || !float.TryParse(args[1], out float y)
                || !float.TryParse(args[2], out float z))
                return CommandResult.Error("usage: tp <x> <y> <z>", "type 'help' for a list of commands");

            return OnTeleport?.Invoke(x, y, z)
                ?? CommandResult.Error("tp: no game context — wire up CommandRegistry.OnTeleport");
        }

        CommandResult DoSay(string[] args)
        {
            if (args.Length < 1)
                return CommandResult.Error("usage: say <message>", "type 'help' for a list of commands");
            return OnSay?.Invoke(string.Join(" ", args))
                ?? CommandResult.Error("say: no game context — wire up CommandRegistry.OnSay");
        }
    }
}
