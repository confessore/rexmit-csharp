// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;

namespace rexmit.Services.Interfaces;

public interface IPlaybackPanelService
{
    Task PublishAsync(ulong guildId, IMessageChannel channel);
    Task RefreshAsync(ulong guildId);
}
