﻿using System.Diagnostics.CodeAnalysis;

namespace GW2EIEvtcParser;

public class CachingCollection<T>(ParsedEvtcLog log) : AbstractCachingCollection<T>(log)
{
    readonly int _initialSecondaryCap = 20;
    private readonly Dictionary<long, Dictionary<long, T>> _cache = new(20);

    public bool TryGetValue(long start, long end, [NotNullWhen(true)] out T? value)
    {
        (start, end) = SanitizeTimes(start, end);
        if (_cache.TryGetValue(start, out var subCache))
        {
            if (subCache.TryGetValue(end, out value!))
            {
                return true;
            }
        }
        value = default;
        return false;
    }
    public bool TryGetEnglobingValue(long start, long end, [NotNullWhen(true)] out T? value)
    {
        (start, end) = SanitizeTimes(start, end);
        var englobingStart = _cache.Keys.Where(x => x <= start && _cache[x].Keys.Any(y => y >= end)).DefaultIfEmpty(0).Max();
        if (_cache.TryGetValue(englobingStart, out var subCache))
        {
            var englobingEnd = subCache.Keys.Where(x => x >= end).DefaultIfEmpty(0).Min();
            if (subCache.TryGetValue(englobingEnd, out value!))
            {
                return true;
            }
        }
        value = default;
        return false;
    }

    public void Set(long start, long end, T value)
    {
        (start, end) = SanitizeTimes(start, end);

        if (!_cache.TryGetValue(start, out var subCache))
        {
            _cache[start] = new Dictionary<long, T>(_initialSecondaryCap);
            subCache = _cache[start];
        }
        subCache[end] = value;
    }

    public bool HasKeys(long start, long end)
    {
        return TryGetValue(start, end, out _);
    }

    public T? Get(long start, long end)
    {
        if (TryGetValue(start, end, out T? value))
        {
            return value;
        }
        return default;
    }

    public override void Clear()
    {
        _cache.Clear();
    }

    public override bool IsEmpty()
    {
        return _cache.Count == 0;
    }
}
