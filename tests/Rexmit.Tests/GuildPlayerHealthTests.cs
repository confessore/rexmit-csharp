// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Discord;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using rexmit.Playback;

namespace Rexmit.Tests;

[TestClass]
public sealed class GuildPlayerHealthTests
{
    [TestMethod]
    public void IsUsableState_RequiresConnectedVoiceAndLiveWorker()
    {
        Assert.IsTrue(GuildPlayer.IsUsableState(ConnectionState.Connected, workerCompleted: false, stopping: false));
        Assert.IsFalse(GuildPlayer.IsUsableState(ConnectionState.Disconnected, workerCompleted: false, stopping: false));
        Assert.IsFalse(GuildPlayer.IsUsableState(ConnectionState.Connecting, workerCompleted: false, stopping: false));
        Assert.IsFalse(GuildPlayer.IsUsableState(ConnectionState.Connected, workerCompleted: true, stopping: false));
        Assert.IsFalse(GuildPlayer.IsUsableState(ConnectionState.Connected, workerCompleted: false, stopping: true));
    }
}
