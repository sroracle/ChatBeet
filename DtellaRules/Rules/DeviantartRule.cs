﻿using ChatBeet;
using DtellaRules.Services;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace DtellaRules.Rules
{
    public class DeviantartRule : MessageRuleBase<IrcMessage>
    {
        private readonly DeviantartService daService;
        private readonly ChatBeetConfiguration config;

        public DeviantartRule(DeviantartService daService, IOptions<ChatBeetConfiguration> options)
        {
            this.daService = daService;
            config = options.Value;
        }

        public override async IAsyncEnumerable<OutboundIrcMessage> Respond(IrcMessage incomingMessage)
        {
            var rgx = new Regex($"^{config.CommandPrefix}(da|deviantart|degenerate) (.*)", RegexOptions.IgnoreCase);
            var match = rgx.Match(incomingMessage.Content);
            if (match.Success)
            {
                var search = match.Groups[2].Value;
                // use ID instead of name if provided
                var media = await daService.GetRecentImageAsync(search);

                if (media != null)
                {
                    yield return new OutboundIrcMessage
                    {
                        Content = $"{IrcValues.BOLD}{media.Title?.Text}{IrcValues.RESET} - {media.Id}",
                        Target = incomingMessage.Channel
                    };
                }
                else
                {
                    yield return new OutboundIrcMessage
                    {
                        Content = $"Sorry, couldn't find anything matching {match.Groups[2].Value}.",
                        Target = incomingMessage.Channel
                    };
                }
            }
        }
    }
}