using System;
using System.Collections.Generic;

namespace FFXIVLoginCommands;

public enum CommandRunMode
{
    EveryLogin = 0,
    OncePerSession = 1
}

public enum CommandStatus
{
    Pending = 0,
    Sent = 1,
    Skipped = 2,
    Error = 3
}

[Serializable]
public sealed class CommandEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public int DelayMs { get; set; } = 0;
    public CommandRunMode RunMode { get; set; } = CommandRunMode.EveryLogin;
    public bool Enabled { get; set; } = true;
}

[Serializable]
public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "New Profile";
    public string CharacterName { get; set; } = string.Empty;
    public ushort WorldId { get; set; } = 0;
    public string WorldName { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public List<CommandEntry> Commands { get; set; } = new();
}

[Serializable]
public sealed class LogEntry
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string CharacterKey { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public CommandStatus Status { get; set; } = CommandStatus.Pending;
    public string Message { get; set; } = string.Empty;
}

[Serializable]
public sealed class SettingsExport
{
    public List<Profile> Profiles { get; set; } = new();
    public List<CommandEntry> GlobalCommands { get; set; } = new();
}
