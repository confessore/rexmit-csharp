// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace rexmit.Services.Interfaces;

public interface IFFmpegService
{
    Task<MemoryStream> CreateStreamAsync(string videoStreamUrl);
    IAsyncEnumerable<string> DownloadVideoAsync(string videoUrl);
}
