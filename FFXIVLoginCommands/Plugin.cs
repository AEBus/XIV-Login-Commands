using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVLoginCommands.Windows;

namespace FFXIVLoginCommands;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/ffxivlogincommands";
    private const int MaxLogEntries = 500;

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("FFXIVLoginCommands");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly List<ExecutionEntry> executionPlan = new();
    private readonly List<ExecutionEntry> pendingQueue = new();
    private readonly HashSet<Guid> sessionExecutedCommands = new();

    public string ActiveCharacterDisplay { get; private set; } = "Not logged in";

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open FFXIV Login Commands"
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        ClientState.Login += OnLogin;
        ClientState.Logout += OnLogout;
        Framework.Update += OnFrameworkUpdate;

        if (ClientState.IsLoggedIn)
        {
            HandleLogin();
        }

        Log.Information($"===FFXIV Login Commands loaded ({PluginInterface.Manifest.Name})===");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        ClientState.Login -= OnLogin;
        ClientState.Logout -= OnLogout;
        Framework.Update -= OnFrameworkUpdate;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    public IReadOnlyList<ExecutionEntry> ExecutionPlan => executionPlan;
    public IReadOnlyList<ExecutionEntry> PendingQueue => pendingQueue;

    private void OnCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    private void OnLogin()
    {
        HandleLogin();
    }

    private void OnLogout(int type, int code)
    {
        ActiveCharacterDisplay = "Not logged in";
        executionPlan.Clear();
        pendingQueue.Clear();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (pendingQueue.Count == 0)
        {
            return;
        }

        var next = pendingQueue[0];
        if (DateTime.UtcNow < next.ScheduledUtc)
        {
            return;
        }

        pendingQueue.RemoveAt(0);
        ExecuteEntry(next);
    }

    private void HandleLogin()
    {
        var characterInfo = GetCurrentCharacterInfo();
        if (characterInfo == null)
        {
            Log.Warning("Login detected but character info is not ready.");
            return;
        }

        ActiveCharacterDisplay = $"{characterInfo.Value.Name} @ {characterInfo.Value.WorldName}";
        BuildExecutionPlan(characterInfo.Value);
    }

    private (string Name, ushort WorldId, string WorldName)? GetCurrentCharacterInfo()
    {
        if (!PlayerState.IsLoaded)
        {
            return null;
        }

        var name = PlayerState.CharacterName;
        var worldId = (ushort)PlayerState.HomeWorld.RowId;
        var worldName = PlayerState.HomeWorld.Value.Name.ToString();
        return (name, worldId, worldName);
    }

    public bool TryGetCurrentCharacterInfo(out (string Name, ushort WorldId, string WorldName) info)
    {
        var result = GetCurrentCharacterInfo();
        if (result == null)
        {
            info = default;
            return false;
        }

        info = result.Value;
        return true;
    }

    private void BuildExecutionPlan((string Name, ushort WorldId, string WorldName) characterInfo)
    {
        executionPlan.Clear();
        pendingQueue.Clear();

        var profile = FindProfile(characterInfo.Name, characterInfo.WorldId);
        var worldDisplay = string.IsNullOrWhiteSpace(characterInfo.WorldName)
            ? $"World {characterInfo.WorldId}"
            : characterInfo.WorldName;
        var characterKey = $"{characterInfo.Name}@{worldDisplay}";

        var entries = new List<CommandEntry>();
        entries.AddRange(Configuration.GlobalCommands);
        if (profile?.Enabled == true)
        {
            entries.AddRange(profile.Commands);
        }

        var scheduledTime = DateTime.UtcNow;
        var sequence = 0;

        foreach (var command in entries)
        {
            var entry = new ExecutionEntry
            {
                SequenceIndex = sequence++,
                CharacterKey = characterKey,
                Command = command,
                ScheduledUtc = scheduledTime
            };

            if (!command.Enabled)
            {
                entry.Status = CommandStatus.Skipped;
                entry.Message = "Disabled";
                AddLog(entry);
                executionPlan.Add(entry);
                continue;
            }

            if (string.IsNullOrWhiteSpace(command.CommandText))
            {
                entry.Status = CommandStatus.Skipped;
                entry.Message = "Empty command";
                AddLog(entry);
                executionPlan.Add(entry);
                continue;
            }

            if (command.RunMode == CommandRunMode.OncePerSession && sessionExecutedCommands.Contains(command.Id))
            {
                entry.Status = CommandStatus.Skipped;
                entry.Message = "Already sent this session";
                AddLog(entry);
                executionPlan.Add(entry);
                continue;
            }

            scheduledTime = scheduledTime.AddMilliseconds(Math.Max(0, command.DelayMs));
            entry.ScheduledUtc = scheduledTime;
            entry.Status = CommandStatus.Pending;
            executionPlan.Add(entry);
            pendingQueue.Add(entry);
        }
    }

    private Profile? FindProfile(string name, ushort worldId)
    {
        return Configuration.Profiles.FirstOrDefault(profile =>
            profile.Enabled &&
            string.Equals(profile.CharacterName, name, StringComparison.OrdinalIgnoreCase) &&
            profile.WorldId == worldId);
    }

    private void ExecuteEntry(ExecutionEntry entry)
    {
        try
        {
            CommandManager.ProcessCommand(entry.Command.CommandText);
            entry.Status = CommandStatus.Sent;
            entry.Message = "Sent";

            if (entry.Command.RunMode == CommandRunMode.OncePerSession)
            {
                sessionExecutedCommands.Add(entry.Command.Id);
            }
        }
        catch (Exception ex)
        {
            entry.Status = CommandStatus.Error;
            entry.Message = ex.Message;
        }

        AddLog(entry);
    }

    public void RunEntryNow(ExecutionEntry entry)
    {
        if (entry.Status != CommandStatus.Pending)
        {
            return;
        }

        pendingQueue.Remove(entry);
        entry.ScheduledUtc = DateTime.UtcNow;
        ExecuteEntry(entry);
    }

    public void SkipEntry(ExecutionEntry entry, string reason)
    {
        if (entry.Status != CommandStatus.Pending)
        {
            return;
        }

        pendingQueue.Remove(entry);
        entry.Status = CommandStatus.Skipped;
        entry.Message = reason;
        AddLog(entry);
    }

    public void ClearPendingQueue()
    {
        foreach (var entry in pendingQueue)
        {
            entry.Status = CommandStatus.Skipped;
            entry.Message = "Cleared";
            AddLog(entry);
        }

        pendingQueue.Clear();
    }

    private void AddLog(ExecutionEntry entry)
    {
        var log = new LogEntry
        {
            TimestampUtc = DateTime.UtcNow,
            CharacterKey = entry.CharacterKey,
            CommandText = entry.Command.CommandText,
            Status = entry.Status,
            Message = entry.Message
        };

        Configuration.Logs.Add(log);
        if (Configuration.Logs.Count > MaxLogEntries)
        {
            Configuration.Logs.RemoveRange(0, Configuration.Logs.Count - MaxLogEntries);
        }

        Configuration.Save();
    }

    public sealed class ExecutionEntry
    {
        public int SequenceIndex { get; set; }
        public string CharacterKey { get; set; } = string.Empty;
        public CommandEntry Command { get; set; } = new();
        public DateTime ScheduledUtc { get; set; } = DateTime.UtcNow;
        public CommandStatus Status { get; set; } = CommandStatus.Pending;
        public string Message { get; set; } = string.Empty;
    }
}
