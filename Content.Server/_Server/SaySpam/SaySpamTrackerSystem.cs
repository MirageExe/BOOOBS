// SPDX-FileCopyrightText: 2026 Amour
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Net.Sockets;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Server.SaySpam;

/// <summary>
/// System for detecting and handling excessive say command usage.
/// </summary>
public sealed class SaySpamTrackerSystem : EntitySystem
{
    [Dependency] private readonly IBanManager _banManager = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    private ISawmill _sawmill = default!;

    private const int MaxMessagesPerMinute = 500;
    private const int BanDurationMinutes = 0;
    private const string AdminName = "Rikardo";
    
    private static readonly Guid AdminGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly NetUserId AdminUserId = new(AdminGuid);

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill("server.saytracker");
    }

    /// <summary>
    /// Checks if a say message should be allowed.
    /// </summary>
    public bool CheckMessage(ICommonSession player, EntityUid entity)
    {
        if (!Resolve(entity, ref var component, false))
        {
            component = AddComp<SaySpamTrackerComponent>(entity);
        }

        if (component.IsActioned)
            return true;

        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-60);

        while (component.MessageTimestamps.Count > 0 && component.MessageTimestamps.Peek() < cutoff)
        {
            component.MessageTimestamps.Dequeue();
        }

        component.MessageTimestamps.Enqueue(now);

        if (component.MessageTimestamps.Count > MaxMessagesPerMinute)
        {
            ApplyAction(player);
            component.IsActioned = true;
            return true;
        }

        return false;
    }

    private async void ApplyAction(ICommonSession player)
    {
        try
        {
            (IPAddress, int)? targetIP = null;
            ImmutableTypedHwid? targetHWid = null;

            var sessionData = await _locator.LookupIdAsync(player.UserId);
            if (sessionData != null)
            {
                if (sessionData.LastAddress is not null)
                {
                    var prefix = sessionData.LastAddress.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
                    targetIP = (sessionData.LastAddress, prefix);
                }
                targetHWid = sessionData.LastHWId;
            }

            var reason = "Нарушение правил сервера";

            _banManager.CreateServerBan(
                player.UserId,
                player.Name,
                AdminUserId,
                targetIP,
                targetHWid,
                BanDurationMinutes <= 0 ? null : (uint)BanDurationMinutes,
                NoteSeverity.High,
                reason
            );

            _sawmill.Info($"{player.Name} ({player.UserId}) actioned for excessive messaging");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to action {player.Name}: {ex}");
        }
    }
}
