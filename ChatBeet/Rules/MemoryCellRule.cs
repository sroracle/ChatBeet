﻿using ChatBeet.Data;
using ChatBeet.Data.Entities;
using ChatBeet.Utilities;
using GravyBot;
using GravyIrc.Messages;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ChatBeet.Rules
{
    public class MemoryCellRule : AsyncMessageRuleBase<PrivateMessage>
    {
        private readonly MemoryCellContext ctx;
        private readonly IrcBotConfiguration config;

        public MemoryCellRule(MemoryCellContext ctx, IOptions<IrcBotConfiguration> options)
        {
            config = options.Value;
            this.ctx = ctx;
        }

        public override bool Matches(PrivateMessage incomingMessage) =>
            new Regex($"^({Regex.Escape(config.Nick)}, |{Regex.Escape(config.CommandPrefix)})(remember (.*?)=(.*))|(recall .*)|(whodef .*)", RegexOptions.IgnoreCase).IsMatch(incomingMessage.Message);

        public override async IAsyncEnumerable<IClientMessage> RespondAsync(PrivateMessage incomingMessage)
        {
            var setRgx = new Regex($"^({Regex.Escape(config.Nick)}, |{Regex.Escape(config.CommandPrefix)})remember (.*?)=(.*)", RegexOptions.IgnoreCase);
            var setMatch = setRgx.Match(incomingMessage.Message);
            if (setMatch.Success)
            {
                var key = setMatch.Groups[2].Value.Trim();
                var value = setMatch.Groups[3].Value.Trim();

                if (string.IsNullOrEmpty(key))
                {
                    yield return new PrivateMessage(
                            incomingMessage.GetResponseTarget(),
                            $"{incomingMessage.From}: provide a name to define."
                        );
                }
                else if (string.IsNullOrEmpty(value))
                {
                    yield return new PrivateMessage(
                            incomingMessage.GetResponseTarget(),
                            $"{incomingMessage.From}: provide a value to set for {IrcValues.BOLD}{key}{IrcValues.RESET}."
                        );
                }
                else
                {
                    var existingCell = await ctx.MemoryCells.FirstOrDefaultAsync(c => c.Key.ToLower() == key.ToLower());
                    if (existingCell != null)
                    {
                        ctx.MemoryCells.Remove(existingCell);
                        await ctx.SaveChangesAsync();
                    }

                    ctx.MemoryCells.Add(new MemoryCell
                    {
                        Author = incomingMessage.From,
                        Key = key,
                        Value = value
                    });
                    await ctx.SaveChangesAsync();

                    yield return new PrivateMessage(incomingMessage.GetResponseTarget(), "Got it! 👍");

                    if (existingCell != null)
                    {
                        yield return new PrivateMessage(
                            incomingMessage.GetResponseTarget(),
                            $"Previous value was {IrcValues.BOLD}{existingCell.Value}{IrcValues.RESET}, set by {existingCell.Author}."
                        );
                    }
                }
            }

            var getRgx = new Regex($"^({Regex.Escape(config.Nick)}, |{Regex.Escape(config.CommandPrefix)})recall (.*)", RegexOptions.IgnoreCase);
            var getMatch = getRgx.Match(incomingMessage.Message);
            if (getMatch.Success)
            {
                var key = getMatch.Groups[2].Value.Trim();

                var cell = await ctx.MemoryCells.FirstOrDefaultAsync(c => c.Key.ToLower() == key.ToLower());

                if (cell != null)
                {
                    yield return new PrivateMessage(
                        incomingMessage.GetResponseTarget(),
                        $"{IrcValues.BOLD}{cell.Key}{IrcValues.RESET}: {cell.Value}"
                    );
                }
                else
                {
                    yield return new PrivateMessage(
                        incomingMessage.GetResponseTarget(),
                        $"I don't have anything for {IrcValues.BOLD}{key}{IrcValues.RESET}."
                    );
                }
            }

            var whoRgx = new Regex($"^({Regex.Escape(config.Nick)}, |{Regex.Escape(config.CommandPrefix)})whodef (.*)", RegexOptions.IgnoreCase);
            var whoMatch = whoRgx.Match(incomingMessage.Message);
            if (whoMatch.Success)
            {
                var key = whoMatch.Groups[2].Value.Trim();

                var cell = await ctx.MemoryCells.FirstOrDefaultAsync(c => c.Key.ToLower() == key.ToLower());

                if (cell != null)
                {
                    yield return new PrivateMessage(
                        incomingMessage.GetResponseTarget(),
                        $"{IrcValues.BOLD}{cell.Key}{IrcValues.RESET} was set by {IrcValues.BOLD}{cell.Author}{IrcValues.RESET}"
                    );
                }
                else
                {
                    yield return new PrivateMessage(
                        incomingMessage.GetResponseTarget(),
                        $"I don't have anything for {IrcValues.BOLD}{key}{IrcValues.RESET}."
                    );
                }
            }
        }
    };
}