// SPDX-FileCopyrightText: 2026 Amour
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._Server.SaySpam;

/// <summary>
/// Tracks say command usage for rate limiting detection.
/// </summary>
[RegisterComponent]
public sealed partial class SaySpamTrackerComponent : Component
{
    /// <summary>
    /// Queue of message timestamps for the sliding window.
    /// </summary>
    [ViewVariables]
    public Queue<DateTime> MessageTimestamps { get; set; } = new();

    /// <summary>
    /// Whether the player has been actioned for spam.
    /// </summary>
    [ViewVariables]
    public bool IsActioned { get; set; } = false;
}
