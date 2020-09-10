﻿using ChatBeet.Data;
using ChatBeet.Data.Entities;
using ChatBeet.Models;
using GravyIrc.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ChatBeet.Services
{
    public class KeywordService
    {
        private readonly KeywordContext db;
        private readonly IMemoryCache cache;

        public KeywordService(KeywordContext db, IMemoryCache cache)
        {
            this.db = db;
            this.cache = cache;
        }

        public Task<List<Keyword>> GetKeywordsAsync() => cache.GetOrCreateAsync("keywords", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            return await db.Keywords.AsQueryable().ToListAsync();
        });

        public async Task RecordKeywordEntryAsync(Keyword keyword, PrivateMessage message)
        {
            var record = new KeywordRecord
            {
                KeywordId = keyword.Id,
                Message = message.Message,
                Nick = message.From,
                Time = message.DateReceived
            };
            db.Records.Add(record);
            await db.SaveChangesAsync();
        }

        public async Task<Keyword> GetKeywordAsync(string label)
        {
            var keywords = await GetKeywordsAsync();
            return keywords.FirstOrDefault(k => k.Label == label);
        }

        public Task<KeywordStat> GetKeywordStat(string label) => cache.GetOrCreateAsync($"keywords:stats:{label}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(5);

            var keyword = await GetKeywordAsync(label);
            var stats = await db.Records
                .AsQueryable()
                .Where(r => r.KeywordId == keyword.Id)
                .GroupBy(r => r.Nick)
                .Select(g => new KeywordStat.UserKeywordStat
                {
                    Nick = g.Key,
                    Hits = g.Count(),
                    Excerpt = g.FirstOrDefault().Message
                })
                .ToListAsync();

            return new KeywordStat
            {
                Keyword = keyword,
                Stats = stats
            };
        });

        public Task<IEnumerable<KeywordStat>> GetKeywordStats() => cache.GetOrCreateAsync("keyword:stats", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(5);

            var keywords = await GetKeywordsAsync();
            var allStats = await db.Records
                .AsQueryable()
                .GroupBy(r => new { r.KeywordId, r.Nick })
                .Select(g => new
                {
                    g.Key.KeywordId,
                    Stats = new KeywordStat.UserKeywordStat
                    {
                        Nick = g.Key.Nick,
                        Hits = g.Count(),
                        Excerpt = g.FirstOrDefault().Message
                    }
                })
                .ToListAsync();
            return keywords.Select(keyword => new KeywordStat
            {
                Keyword = keyword,
                Stats = allStats
                    .Where(s => s.KeywordId == keyword.Id)
                    .Select(s => s.Stats)
            });
        });
    }
}
