using System.Security.Cryptography;
using System.Threading;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace VisualSearch.Api.Services;

/// <summary>
/// Issues short-lived, one-time tickets for authenticating SSE connections.
/// </summary>
public sealed class SseTicketService
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly ILogger<SseTicketService> _logger;

    public SseTicketService(IMemoryCache cache, ILogger<SseTicketService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public SseTicketIssueResult Issue(string purpose, string? subject = null, TimeSpan? lifetime = null)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Purpose is required", nameof(purpose));
        }

        var bytes = RandomNumberGenerator.GetBytes(32);
        var ticket = WebEncoders.Base64UrlEncode(bytes);

        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime ?? DefaultLifetime);
        var entry = new SseTicketEntry(purpose, subject, expiresAt);

        _cache.Set(GetCacheKey(ticket), entry, expiresAt);

        return new SseTicketIssueResult(ticket, expiresAt);
    }

    public bool TryConsume(string purpose, string ticket, out SseTicketInfo info)
    {
        info = default;

        if (string.IsNullOrWhiteSpace(purpose) || string.IsNullOrWhiteSpace(ticket))
        {
            return false;
        }

        if (!_cache.TryGetValue(GetCacheKey(ticket), out SseTicketEntry? entry) || entry is null)
        {
            return false;
        }

        if (!string.Equals(entry.Purpose, purpose, StringComparison.Ordinal))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow >= entry.ExpiresAt)
        {
            _cache.Remove(GetCacheKey(ticket));
            return false;
        }

        if (Interlocked.Exchange(ref entry.Consumed, 1) == 1)
        {
            return false;
        }

        _cache.Remove(GetCacheKey(ticket));

        info = new SseTicketInfo(entry.Purpose, entry.Subject, entry.ExpiresAt);
        return true;
    }

    private static string GetCacheKey(string ticket) => $"sse-ticket:{ticket}";

    private sealed class SseTicketEntry
    {
        public SseTicketEntry(string purpose, string? subject, DateTimeOffset expiresAt)
        {
            Purpose = purpose;
            Subject = subject;
            ExpiresAt = expiresAt;
        }

        public string Purpose { get; }
        public string? Subject { get; }
        public DateTimeOffset ExpiresAt { get; }
        public int Consumed;
    }
}

public readonly record struct SseTicketIssueResult(string Ticket, DateTimeOffset ExpiresAt);

public readonly record struct SseTicketInfo(string Purpose, string? Subject, DateTimeOffset ExpiresAt);
