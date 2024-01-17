﻿// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;

namespace rexmit.Services.Interfaces;

public interface IFFmpegService
{
    Process CreateStream(string path);
    IAsyncEnumerable<string> DownloadVideoAsync(string videoUrl);
}