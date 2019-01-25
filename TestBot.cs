// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace dchTestBot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class TestBot : IBot
    {
        private readonly TestBotAccessors _accessors;
        private readonly MessageContext _context;
        private readonly ILogger _logger;
        private readonly Random _random = new Random();
        private readonly TelemetryClient _telemetry = new TelemetryClient();

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="accessors">A class containing <see cref="IStatePropertyAccessor{T}"/> used to manage state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
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

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all t he data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
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

        private void TrackMessage(ITurnContext turnContext)
        {
            if (string.Empty != turnContext.Activity.Text)
            {
                var properties = new Dictionary<string, string> { { "BotQuestion", turnContext.Activity.Text } };
                if (turnContext.Activity.From != null)
                {
                    properties.Add("Name", turnContext.Activity.From.Name);
                    properties.Add("Channel", turnContext.Activity.ChannelId);
                    properties.Add("ConversationId", turnContext.Activity.Conversation.Id);
                }
                _telemetry.TrackEvent("BotQuestion", properties);
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
    }
}