using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus.VoiceLink.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Serilog.Sinks.SystemConsole.Themes;

namespace DSharpPlus.VoiceLink.Examples.HelloWorld
{
    public sealed class Program
    {
        public static async Task Main()
        {
            IServiceCollection services = new ServiceCollection().AddLogging(logger =>
            {
                string loggingFormat = "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u4}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

                // Log both to console and the file
                LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
                .MinimumLevel.Is(LogEventLevel.Verbose)
                .Filter.ByIncludingOnly(Matching.FromSource("DSharpPlus.VoiceLink"))
                .WriteTo.Console(outputTemplate: loggingFormat, theme: new AnsiConsoleTheme(new Dictionary<ConsoleThemeStyle, string>
                {
                    [ConsoleThemeStyle.Text] = "\x1b[0m",
                    [ConsoleThemeStyle.SecondaryText] = "\x1b[90m",
                    [ConsoleThemeStyle.TertiaryText] = "\x1b[90m",
                    [ConsoleThemeStyle.Invalid] = "\x1b[31m",
                    [ConsoleThemeStyle.Null] = "\x1b[95m",
                    [ConsoleThemeStyle.Name] = "\x1b[93m",
                    [ConsoleThemeStyle.String] = "\x1b[96m",
                    [ConsoleThemeStyle.Number] = "\x1b[95m",
                    [ConsoleThemeStyle.Boolean] = "\x1b[95m",
                    [ConsoleThemeStyle.Scalar] = "\x1b[95m",
                    [ConsoleThemeStyle.LevelVerbose] = "\x1b[34m",
                    [ConsoleThemeStyle.LevelDebug] = "\x1b[90m",
                    [ConsoleThemeStyle.LevelInformation] = "\x1b[36m",
                    [ConsoleThemeStyle.LevelWarning] = "\x1b[33m",
                    [ConsoleThemeStyle.LevelError] = "\x1b[31m",
                    [ConsoleThemeStyle.LevelFatal] = "\x1b[97;91m"
                }))
                .WriteTo.File(
                    $"logs/{DateTime.Now.ToUniversalTime().ToString("yyyy'-'MM'-'dd' 'HH'_'mm'_'ss", CultureInfo.InvariantCulture)}.log",
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: loggingFormat
                );

                // Set Log.Logger for a static reference to the logger
                logger.AddSerilog(loggerConfiguration.CreateLogger());
            });

            DiscordClient client = new(new()
            {
                Token = Environment.GetEnvironmentVariable("DISCORD_TOKEN") ?? throw new InvalidOperationException("DISCORD_TOKEN environment variable is not set or is incorrect."),
                Intents = DiscordIntents.All,
                LoggerFactory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>()
            });

            VoiceLinkExtension voiceLinkExtension = client.UseVoiceLink(new VoiceLinkConfiguration()
            {
                ServiceCollection = services
            });

            voiceLinkExtension.UserSpeaking += async (sender, e) =>
            {
                FileStream fileStream = File.Open($"test/{e.Member.Id}.pcm", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                await e.VoiceUser.AudioStream.CopyToAsync(fileStream);
                await fileStream.DisposeAsync();
            };

            client.GuildDownloadCompleted += async (sender, e) =>
            {
                if (!ulong.TryParse(Environment.GetEnvironmentVariable("DISCORD_GUILD"), out ulong guildId))
                {
                    throw new InvalidOperationException("DISCORD_GUILD environment variable is not set or is incorrect.");
                }

                // no else if cause of scope
                if (!ulong.TryParse(Environment.GetEnvironmentVariable("DISCORD_CHANNEL"), out ulong channelId))
                {
                    throw new InvalidOperationException("DISCORD_CHANNEL environment variable is not set or is incorrect.");
                }

                await voiceLinkExtension.ConnectAsync(sender.Guilds[guildId].Channels[channelId], VoiceState.None);
            };

            await client.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}
