using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Manimal.QuestBriefingAPI
{
    internal sealed class BriefingRecording
    {
        internal string OwnerId, QuestId, TraderId, Directory, File;
        internal bool? RadioFilter, RadioCues;

        internal string ResolvePath()
        {
            // Revalidate at playback, so replacing a directory with a link cannot escape the pack.
            var path = BriefingRegistry.SafePath(Directory, File);
            if (Path.HasExtension(path)) return System.IO.File.Exists(path) ? path : null;
            foreach (var extension in new[] { ".wav", ".ogg", ".mp3" })
            {
                var candidate = BriefingRegistry.SafePath(Directory, File + extension);
                if (System.IO.File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }

    internal sealed class BriefingRegistry
    {
        private readonly Dictionary<string, BriefingRecording> _recordings = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _packs = new(StringComparer.OrdinalIgnoreCase);
        internal Action<string> Warning = _ => { };
        internal int Count => _recordings.Count;

        internal BriefingRecording Find(string questId, string traderId)
        {
            if (questId == null || !_recordings.TryGetValue(questId, out var recording)) return null;
            return recording.TraderId == null || string.Equals(recording.TraderId, traderId,
                StringComparison.OrdinalIgnoreCase) ? recording : null;
        }

        internal bool Register(string ownerId, string questId, string directory, string file,
            string traderId = null, bool? radioFilter = null, bool? radioCues = null)
        {
            try { return Add(Create(ownerId, questId, directory, file, traderId, radioFilter, radioCues)); }
            catch (Exception ex)
            {
                Warning($"[Briefings] Invalid registration from '{ownerId}' for '{questId}': {ex.Message}");
                return false;
            }
        }

        private bool Add(BriefingRecording recording)
        {
            if (_recordings.TryGetValue(recording.QuestId, out var existing))
            {
                Warning($"[Briefings] Quest {recording.QuestId}: keeping '{existing.OwnerId}', ignoring '{recording.OwnerId}'.");
                return false;
            }
            _recordings.Add(recording.QuestId, recording);
            return true;
        }

        internal bool Unregister(string ownerId, string questId)
        {
            if (questId == null || !_recordings.TryGetValue(questId, out var recording)
                || !string.Equals(ownerId, recording.OwnerId, StringComparison.Ordinal)) return false;
            return _recordings.Remove(questId);
        }

        internal int LoadPacks(string pluginsDirectory)
        {
            var manifests = new List<string>();
            Discover(pluginsDirectory, manifests);
            manifests.Sort(StringComparer.Ordinal);
            var loaded = 0;
            foreach (var manifest in manifests)
                if (LoadPack(manifest)) loaded++;
            return loaded;
        }

        private void Discover(string directory, List<string> manifests)
        {
            try
            {
                if ((System.IO.File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return;
                var manifest = Path.Combine(directory, "briefings.json");
                if (System.IO.File.Exists(manifest)) manifests.Add(manifest);
                foreach (var child in System.IO.Directory.GetDirectories(directory)) Discover(child, manifests);
            }
            catch (Exception ex) { Warning($"[Briefings] Cannot scan '{directory}': {ex.Message}"); }
        }

        internal bool LoadPack(string manifest)
        {
            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(manifest));
                SafePath(directory, Path.GetFileName(manifest), false);
                // Catch duplicate JSON keys and misspelled settings rather than silently ignoring them.
                var json = JObject.Parse(System.IO.File.ReadAllText(manifest), new JsonLoadSettings
                { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                var pack = json.ToObject<Pack>(JsonSerializer.Create(new JsonSerializerSettings
                { MissingMemberHandling = MissingMemberHandling.Error }));
                if (pack.SchemaVersion != 1) throw new InvalidDataException("schemaVersion must be 1.");
                if (string.IsNullOrWhiteSpace(pack.PackId)) throw new InvalidDataException("packId is required.");
                if (_packs.Contains(pack.PackId)) throw new InvalidDataException($"Duplicate packId '{pack.PackId}'.");
                if (pack.Briefings == null || pack.Briefings.Length == 0)
                    throw new InvalidDataException("briefings must contain at least one recording.");
                var pending = new List<BriefingRecording>();
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in pack.Briefings)
                {
                    if (entry == null) throw new InvalidDataException("A briefing entry cannot be null.");
                    var recording = Create(pack.PackId, entry.QuestId, directory, entry.File,
                        entry.TraderId ?? pack.TraderId, entry.RadioFilter ?? pack.RadioFilter, entry.RadioCues ?? pack.RadioCues);
                    if (!ids.Add(recording.QuestId)) throw new InvalidDataException($"Repeated questId '{recording.QuestId}' in pack.");
                    pending.Add(recording);
                }
                // Validation is atomic: a broken pack cannot leave a half-registered set of quests.
                _packs.Add(pack.PackId);
                foreach (var recording in pending) Add(recording);
                return true;
            }
            catch (Exception ex) { Warning($"[Briefings] Skipping '{manifest}': {ex.Message}"); return false; }
        }

        private static BriefingRecording Create(string ownerId, string questId, string directory, string file,
            string traderId, bool? radioFilter, bool? radioCues)
        {
            if (string.IsNullOrWhiteSpace(ownerId)) throw new ArgumentException("ownerId is required.");
            if (!IsId(questId)) throw new ArgumentException("questId must contain 24 hexadecimal characters.");
            if (traderId != null && !IsId(traderId)) throw new ArgumentException("traderId must contain 24 hexadecimal characters.");
            SafePath(directory, file);
            return new BriefingRecording { OwnerId = ownerId, QuestId = questId, TraderId = traderId,
                Directory = Path.GetFullPath(directory), File = file, RadioFilter = radioFilter, RadioCues = radioCues };
        }

        private static bool IsId(string id)
        {
            if (id == null || id.Length != 24) return false;
            foreach (var c in id) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        internal static string SafePath(string directory, string file, bool audio = true)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)
                || directory.StartsWith(@"\\") || directory.StartsWith("//"))
                throw new ArgumentException("An absolute local directory is required.");
            if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file) || file.IndexOf(':') >= 0)
                throw new ArgumentException("file must be a relative local path.");
            foreach (var part in file.Replace('\\', '/').Split('/'))
                if (part == ".." || part == "." || part.Length == 0 || part.EndsWith(" ") || part.EndsWith("."))
                    throw new ArgumentException("file contains an unsafe path segment.");
            var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(root, file));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("file escapes its pack directory.");
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (audio && extension != "" && extension != ".wav" && extension != ".ogg" && extension != ".mp3")
                throw new ArgumentException("Only WAV, OGG, MP3 or extensionless audio paths are supported.");
            for (var cursor = path; cursor != null; cursor = Path.GetDirectoryName(cursor))
                if ((System.IO.File.Exists(cursor) || System.IO.Directory.Exists(cursor))
                    && (System.IO.File.GetAttributes(cursor) & FileAttributes.ReparsePoint) != 0)
                    throw new ArgumentException("Symbolic links and junctions are not supported in recording paths.");
            return path;
        }

        private sealed class Pack
        {
            public int SchemaVersion { get; set; }
            public string PackId { get; set; }
            public string TraderId { get; set; }
            public bool? RadioFilter { get; set; }
            public bool? RadioCues { get; set; }
            public Entry[] Briefings { get; set; }
        }
        private sealed class Entry
        {
            public string QuestId { get; set; }
            public string File { get; set; }
            public string TraderId { get; set; }
            public bool? RadioFilter { get; set; }
            public bool? RadioCues { get; set; }
        }
    }
}
