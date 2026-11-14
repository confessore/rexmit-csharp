// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using rexmit.Services.Interfaces;

namespace rexmit.Media;

public interface IYouTubeTrackReader
{
    Task<PlaybackTrack> ReadAsync(string url, string requestedBy);
}
