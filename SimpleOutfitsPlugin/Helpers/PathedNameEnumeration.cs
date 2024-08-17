using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;

namespace SimpleOutfitsPlugin.Helpers;

public static class PathedNameEnumeration {
    private record F<T> {
        private readonly Dictionary<string, F<T>> _subFolders = new();

        public F<T> GetOrCreateSubFolder(string f) {
            if (_subFolders.TryGetValue(f, out var s)) return s;
            return _subFolders[f] = new F<T>();
        }

        public readonly List<T> Items = [];

        public IEnumerable<T> DrawTree() {
            foreach (var f in _subFolders) {
                using var _ = ImRaii.PushId((int)ImGui.GetID(f.Key));
                if (ImGui.TreeNode(f.Key)) {
                    foreach (var i in f.Value.DrawTree()) yield return i;

                    ImGui.TreePop();
                }
            }

            foreach (var i in Items) yield return i;
        }
    }

    private static readonly char[] Separator = ['/', '\\'];

    public static IEnumerable<(string, T)> DrawFolderTree<T>(this IReadOnlyDictionary<string, T> self) where T : IPathedName {
        var root = new F<(string, T)>();
        foreach (var s in self) {
            var p = s.Value.NameWithPath.TrimStart('/', '\\').Split(Separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).SkipLast(1);
            var f = root;
            foreach (var a in p) f = f.GetOrCreateSubFolder(a);

            f.Items.Add((s.Key, s.Value));
        }

        foreach (var k in root.DrawTree()) yield return k;
    }
}
