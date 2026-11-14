// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using rexmit.Playback;
using rexmit.Services.Interfaces;

namespace Rexmit.Tests;

[TestClass]
public sealed class QueueOrderTests
{
    [TestMethod]
    public void MoveOne_ReordersByStableTrackIdAndRejectsStaleOrOutOfRangeMoves()
    {
        var first = CreateTrack("first");
        var second = CreateTrack("second");
        var third = CreateTrack("third");
        var queue = new List<PlaybackTrack> { first, second, third };

        Assert.IsTrue(QueueOrder.MoveOne(queue, third.Id, -1));
        CollectionAssert.AreEqual(new[] { first.Id, third.Id, second.Id }, queue.Select(track => track.Id).ToArray());
        Assert.IsFalse(QueueOrder.MoveOne(queue, first.Id, -1));
        Assert.IsFalse(QueueOrder.MoveOne(queue, Guid.NewGuid(), -1));
    }

    private static PlaybackTrack CreateTrack(string title) => new(
        Guid.NewGuid(),
        $"https://youtube.com/watch?v={title}",
        title,
        null,
        null,
        TimeSpan.FromMinutes(1),
        false,
        "test"
    );
}
