// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using rexmit.Modules.Playback;
using rexmit.Services.Interfaces;

namespace Rexmit.Tests;

[TestClass]
public sealed class PlaybackPanelRendererTests
{
    [TestMethod]
    public void CreateEmbed_ShowsPositionAndQueuedTrack()
    {
        var current = new PlaybackTrack(
            Guid.NewGuid(),
            "https://youtube.com/watch?v=current",
            "Current track",
            "Example artist",
            null,
            TimeSpan.FromMinutes(3),
            false,
            "Requester"
        );
        var queued = new PlaybackTrack(
            Guid.NewGuid(),
            "https://youtube.com/watch?v=queued",
            "Next track",
            null,
            null,
            TimeSpan.FromSeconds(95),
            false,
            "Requester"
        );
        var status = new GuildPlaybackStatus(1, 2, current, [queued], TimeSpan.FromSeconds(83), false);

        var embed = PlaybackPanelRenderer.CreateEmbed(status);

        Assert.AreEqual("Current track", embed.Title);
        StringAssert.Contains(embed.Description, "1:23 / 3:00");
        Assert.AreEqual("Up next · 1 track(s)", embed.Fields.Single().Name);
        StringAssert.Contains(embed.Fields.Single().Value as string, "Next track");

        var removeOption = PlaybackPanelRenderer.CreateComponents(status).Components
            .OfType<ActionRowComponent>()
            .SelectMany(row => row.Components)
            .OfType<SelectMenuComponent>()
            .Single()
            .Options.Single();
        Assert.AreEqual(queued.Id.ToString("N"), removeOption.Value);
    }
}
