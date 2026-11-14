// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using rexmit.Services.Interfaces;

namespace rexmit.Playback;

internal static class QueueOrder
{
    public static bool MoveOne(List<PlaybackTrack> queue, Guid trackId, int offset)
    {
        if (offset is not (-1 or 1))
        {
            return false;
        }

        var index = queue.FindIndex(track => track.Id == trackId);
        var destination = index + offset;
        if (index < 0 || destination < 0 || destination >= queue.Count)
        {
            return false;
        }

        (queue[index], queue[destination]) = (queue[destination], queue[index]);
        return true;
    }
}
