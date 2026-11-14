// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using rexmit.Media;

namespace Rexmit.Tests;

[TestClass]
public sealed class YouTubeUrlTests
{
    [DataTestMethod]
    [DataRow("https://youtube.com/watch?v=example")]
    [DataRow("https://music.youtube.com/watch?v=example")]
    [DataRow("https://youtu.be/example")]
    public void Validate_AllowsSupportedHttpsHosts(string url)
    {
        YouTubeUrl.Validate(url);
    }

    [DataTestMethod]
    [DataRow("http://youtube.com/watch?v=example")]
    [DataRow("https://youtube.com.example.invalid/watch?v=example")]
    [DataRow("https://user@youtube.com/watch?v=example")]
    [DataRow("https://youtube.com:444/watch?v=example")]
    [DataRow("")]
    public void Validate_RejectsUnsupportedUrls(string url)
    {
        Assert.ThrowsException<ArgumentException>(() => YouTubeUrl.Validate(url));
    }
}
