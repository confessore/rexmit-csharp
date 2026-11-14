// Copyright (c) Balanced Solutions Software. All Rights Reserved. Licensed under the MIT license. See LICENSE in the project root for license information.

using Microsoft.Extensions.Hosting;
using rexmit.Hosting;

namespace rexmit;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var token = builder.Configuration["DISCORD_TOKEN"];
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Set the DISCORD_TOKEN environment variable before starting the bot.");
        }

        builder.Services.AddRexmit(token);
        await builder.Build().RunAsync();
    }
}
