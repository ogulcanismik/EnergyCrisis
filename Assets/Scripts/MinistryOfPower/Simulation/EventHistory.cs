using System.Collections.Generic;
using System.Text;

namespace MinistryOfPower.Simulation
{
    public sealed class EventHistoryEntry
    {
        public string Title;
        public string SeasonTag;
        public string DateLabel;
        public string ChoiceSummary;
    }

    /// <summary>Ring buffer of recent crisis cards.</summary>
    public sealed class EventHistory
    {
        public const int Cap = 20;
        private readonly List<EventHistoryEntry> _entries = new List<EventHistoryEntry>(Cap);

        public IReadOnlyList<EventHistoryEntry> Entries => _entries;

        public void Record(PendingEvent evt, GameClock clock, string choiceSummary = null)
        {
            if (evt == null) return;
            _entries.Insert(0, new EventHistoryEntry
            {
                Title = evt.Title,
                SeasonTag = clock != null ? clock.CurrentSeason.ToString() : "?",
                DateLabel = clock != null ? clock.FormatDate() : "?",
                ChoiceSummary = choiceSummary ?? (evt.AwaitingDecision ? "open" : "resolved")
            });
            while (_entries.Count > Cap) _entries.RemoveAt(_entries.Count - 1);
        }

        public void UpdateLatestChoice(string summary)
        {
            if (_entries.Count == 0 || string.IsNullOrEmpty(summary)) return;
            _entries[0].ChoiceSummary = summary;
        }

        public string FormatPanel()
        {
            var sb = new StringBuilder(768);
            sb.AppendLine("EVENT HISTORY (latest first)");
            if (_entries.Count == 0)
            {
                sb.AppendLine("· No cards yet. Enjoy the silence.");
                return sb.ToString();
            }

            int n = _entries.Count < 12 ? _entries.Count : 12;
            for (int i = 0; i < n; i++)
            {
                EventHistoryEntry e = _entries[i];
                sb.Append("· ").Append(e.DateLabel).Append(" [").Append(e.SeasonTag).Append("] ")
                    .Append(e.Title);
                if (!string.IsNullOrEmpty(e.ChoiceSummary))
                    sb.Append(" — ").Append(e.ChoiceSummary);
                sb.Append('\n');
            }

            return sb.ToString();
        }
    }
}
