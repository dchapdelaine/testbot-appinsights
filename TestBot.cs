using Microsoft.ApplicationInsights;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace dchTestBot
{
    public class TestBot : IBot
    {
        private readonly TestBotAccessors _accessors;
        private readonly MessageContext _context;
        private readonly ILogger _logger;
        private readonly Random _random = new Random();
        private readonly TelemetryClient _telemetry = new TelemetryClient();

        public TestBot(TestBotAccessors accessors, ILoggerFactory loggerFactory, MessageContext context)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _context = context;
            _logger = loggerFactory.CreateLogger<TestBot>();
            _logger.LogTrace("Turn start.");
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
        }

        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                if (turnContext.Activity.Type == ActivityTypes.Message)
                {
                    var receivedMessage = turnContext.Activity.Text;
                    TrackMessage(turnContext);

                    var message = string.Empty;
                    if (receivedMessage == "error")
                    {
                        ThrowingMethod();
                    }
                    else if (receivedMessage == "slow")
                    {
                        var delay = await SlowMethod();
                        message = $"That was slow... {delay}ms";
                    }
                    else if (receivedMessage.StartsWith("save "))
                    {
                        var subString = receivedMessage.Substring(5);
                        await SavetoDB(subString);
                        message = $"Saved {subString} to the DB";
                    }
                    else if (receivedMessage.StartsWith("load"))
                    {
                        var loadedMessages = await LoadFromDB();
                        message = "Loaded these messages from the DB:\n";
                        foreach (var loadedMessage in loadedMessages)
                        {
                            message += loadedMessage + "\n";
                        }
                    }
                    else
                    {
                        message = $"You sent '{receivedMessage}'";
                    }

                    // Echo back to the user whatever they typed.
                    await turnContext.SendActivityAsync(message);
                }
                else
                {
                    await turnContext.SendActivityAsync($"{turnContext.Activity.Type} event detected");
                }
            }
            catch (Exception ex)
            {
                _telemetry.TrackException(ex);
                throw;
            }
        }

        private async Task<IEnumerable<string>> LoadFromDB()
        {
            return await _context.Messages
                .OrderByDescending(m => m.MessageId)
                .Take(5)
                .Select(m => m.Content)
                .ToListAsync();
        }

        private async Task SavetoDB(string v)
        {
            await _context.Messages.AddAsync(new Message
            {
                Content = v,
                Time = DateTime.Now
            });

            await _context.SaveChangesAsync();
        }

        private async Task<int> SlowMethod()
        {
            var delay = (int)(_random.NextDouble() * 5000);
            await Task.Delay(delay).ConfigureAwait(false);
            return delay;
        }

        private void ThrowingMethod()
        {
            throw new Exception("Some random exception");
        }

        private void TrackMessage(ITurnContext turnContext)
        {
            if (string.Empty != turnContext.Activity.Text)
            {
                var properties = new Dictionary<string, string>
                {
                    { "BotQuestion", turnContext.Activity.Text },
                    { "Name", turnContext.Activity.From?.Name },
                    { "Channel", turnContext.Activity.ChannelId },
                    { "ConversationId", turnContext.Activity.Conversation?.Id }
                };
                _telemetry.TrackEvent("BotQuestion", properties);
            }
        }
    }
}