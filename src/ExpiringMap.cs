using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlickerDreams;

// Game object pointer -> value for a few seconds (far longer than a wave lives). The expiry keeps an entry from outliving
// an object that is destroyed instead of pooled, whose memory something else may reuse.
internal sealed class ExpiringMap<TValue>(float seconds) {
    private readonly Dictionary<IntPtr, (TValue Value, float Until)> _entries = [];

    public int Count => _entries.Count;

    public void Set(IntPtr key, TValue value) {
        float now = Time.time;
        if (_entries.Count > 32) {
            var expired = new List<IntPtr>();
            foreach (KeyValuePair<IntPtr, (TValue Value, float Until)> entry in _entries) {
                if (entry.Value.Until < now) {
                    expired.Add(entry.Key);
                }
            }

            expired.ForEach(expiredKey => _entries.Remove(expiredKey));
        }

        _entries[key] = (value, now + seconds);
    }

    public bool TryGet(IntPtr key, out TValue value) {
        value = default;
        if (!_entries.TryGetValue(key, out (TValue Value, float Until) entry)) {
            return false;
        }

        if (entry.Until < Time.time) {
            _entries.Remove(key);
            return false;
        }

        value = entry.Value;
        return true;
    }

    public void Remove(IntPtr key) => _entries.Remove(key);
}
