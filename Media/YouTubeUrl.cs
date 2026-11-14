// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

namespace rexmit.Media;

internal static class YouTubeUrl
{
    public static void Validate(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048 || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Provide a valid YouTube video URL.", nameof(url));
        }

        var host = uri.Host;
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || !string.IsNullOrEmpty(uri.UserInfo) ||
            !(host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
              host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase) ||
              host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "Only https://youtube.com, https://music.youtube.com, and https://youtu.be links are supported.",
                nameof(url)
            );
        }
    }
}
