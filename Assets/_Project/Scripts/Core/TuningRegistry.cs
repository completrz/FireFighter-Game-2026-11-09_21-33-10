using System;
using System.Collections.Generic;
using System.Reflection;

namespace Firehouse.Core
{
    /// <summary>Keeps track of loaded tuning assets and the [Tunable] fields on each.</summary>
    public static class TuningRegistry
    {
        public sealed class Entry
        {
            public TunableData Asset;
            public List<(FieldInfo field, TunableAttribute range)> Fields;
        }

        private static readonly List<Entry> s_entries = new();
        private static readonly Dictionary<Type, List<(FieldInfo, TunableAttribute)>> s_fieldCache = new();

        /// <summary>Extra panel sections drawn with IMGUI (network status, cheats...).</summary>
        private static readonly List<(string title, Action draw)> s_sections = new();

        public static IReadOnlyList<Entry> Entries => s_entries;
        public static IReadOnlyList<(string title, Action draw)> Sections => s_sections;

        public static void Register(TunableData asset)
        {
            if (asset == null) return;
            foreach (var e in s_entries)
                if (e.Asset == asset) return;
            s_entries.Add(new Entry { Asset = asset, Fields = FieldsFor(asset.GetType()) });
            s_entries.Sort((a, b) => string.CompareOrdinal(a.Asset.DebugCategory, b.Asset.DebugCategory));
        }

        public static void Unregister(TunableData asset) => s_entries.RemoveAll(e => e.Asset == asset);

        public static void AddSection(string title, Action draw)
        {
            RemoveSection(title);
            s_sections.Add((title, draw));
        }

        public static void RemoveSection(string title) => s_sections.RemoveAll(s => s.title == title);

        private static List<(FieldInfo, TunableAttribute)> FieldsFor(Type t)
        {
            if (s_fieldCache.TryGetValue(t, out var list)) return list;
            list = new List<(FieldInfo, TunableAttribute)>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            // Walk base classes too so private fields of base types are found.
            for (var type = t; type != null && type != typeof(TunableData); type = type.BaseType)
            {
                foreach (var f in type.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    var attr = f.GetCustomAttribute<TunableAttribute>();
                    if (attr == null) continue;
                    if (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool))
                        list.Add((f, attr));
                }
            }
            s_fieldCache[t] = list;
            return list;
        }
    }
}
