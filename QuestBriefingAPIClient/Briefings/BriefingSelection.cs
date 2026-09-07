using System;
using System.IO;

namespace Manimal.QuestBriefingAPI
{
    // Independent of Unity so selection/loading races can be exercised without a game process.
    public sealed class BriefingSelection
    {
        public string QuestId { get; private set; }
        public int Revision { get; private set; }
        public bool WantsPlayback { get; private set; }

        public bool Select(string questId, bool autoplay)
        {
            if (QuestId == questId) return false;
            QuestId = questId;
            Revision++;
            WantsPlayback = questId != null && autoplay;
            return true;
        }

        public void Play() => WantsPlayback = QuestId != null;
        public void Stop() => WantsPlayback = false;
        public bool IsCurrent(int revision) => QuestId != null && revision == Revision;
        public void Clear() { QuestId = null; Revision++; WantsPlayback = false; }

        public static string FindRecording(string directory, string questId)
        {
            // Quest IDs are filenames, never arbitrary paths supplied by a quest template.
            if (questId == null || questId.Length != 24) return null;
            foreach (var c in questId)
                if (!Uri.IsHexDigit(c)) return null;
            foreach (var extension in new[] { ".wav", ".ogg", ".mp3" })
            {
                var path = Path.Combine(directory, questId + extension);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        public static string FormatTime(float seconds)
        {
            var total = float.IsNaN(seconds) || float.IsInfinity(seconds) ? 0 : (int)Math.Max(0, seconds);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }
}
