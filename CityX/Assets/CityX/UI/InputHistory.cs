using System.Collections.Generic;

namespace CityX.UI
{
    /// <summary>
    /// What was typed before, walked with the arrow keys as in a terminal (owner, 2026-10-24: "if i press the up key in console then it retypes
    /// my last message out and if i keep going then it keeps scrolling through my previous messages, make the same function for text chat but
    /// keep the text chat and console chat history separate"). Up steps back to older lines, Down forward again and finally back to what was
    /// being typed before the walk began. The console and the chat each keep their own.
    /// </summary>
    public sealed class InputHistory
    {
        /// <summary>The oldest lines go past this many.</summary>
        public const int MaxLines = 50;

        private readonly List<string> _lines = new List<string>();

        /// <summary>Where the walk is: _lines.Count when not walking (the line being typed).</summary>
        private int _cursor;

        private string _draft = string.Empty;

        public int Count => _lines.Count;

        /// <summary>Remembers a sent line (an empty one, or the same as the last, is not kept twice) and ends any walk.</summary>
        public void Add(string line)
        {
            string trimmed = line != null ? line.Trim() : string.Empty;
            if (trimmed.Length > 0 && (_lines.Count == 0 || _lines[_lines.Count - 1] != trimmed))
            {
                _lines.Add(trimmed);
                if (_lines.Count > MaxLines)
                {
                    _lines.RemoveAt(0);
                }
            }
            _cursor = _lines.Count;
            _draft = string.Empty;
        }

        /// <summary>Up: the line before the one shown (<paramref name="current"/> is what the box holds now, kept as the draft when a walk starts).</summary>
        public string Older(string current)
        {
            if (_lines.Count == 0)
            {
                return current;
            }
            if (_cursor >= _lines.Count)
            {
                _draft = current ?? string.Empty;
                _cursor = _lines.Count;
            }
            if (_cursor > 0)
            {
                _cursor--;
            }
            return _lines[_cursor];
        }

        /// <summary>Down: the line after the one shown, and past the newest, the draft again.</summary>
        public string Newer(string current)
        {
            if (_cursor >= _lines.Count)
            {
                return current;
            }
            _cursor++;
            return _cursor < _lines.Count ? _lines[_cursor] : _draft;
        }

        /// <summary>Forgets where the walk was (the box was opened afresh), keeping the lines.</summary>
        public void EndWalk()
        {
            _cursor = _lines.Count;
            _draft = string.Empty;
        }
    }
}
