using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DSharpPlus.VoiceLink
{
    public class VoiceLinkErrorHandler : IClientErrorHandler
    {
        public static readonly VoiceLinkErrorHandler Instance = new();

        public ValueTask HandleGatewayError(Exception exception) => ValueTask.CompletedTask;
        public ValueTask HandleEventHandlerError(string name, Exception exception, Delegate invokedDelegate, object sender, object args)
        {
            if (sender is VoiceLinkExtension extension)
            {
                extension._logger.LogError(exception, "Event handler '{Method}' for event {AsyncEvent} threw an unhandled exception.", invokedDelegate.Method, name);
            }

            return ValueTask.CompletedTask;
        }
    }
}
