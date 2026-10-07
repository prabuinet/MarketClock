using System.Windows.Input;

namespace MarketClock.Panels
{
    // The calculator half of the Notes & Calculator panel.
    //   > 10 + 20 + 30 + 50 / 2
    //   = 85
    // A line starting with ">" is worked out when Enter is pressed on it, and the answer is
    // written on the line below. Pressing Enter on it again (after editing it) updates the
    // answer in place. Leaving the notes box does the same for every ">" line, which covers
    // pasted text. The arithmetic itself lives in ExpressionEvaluator.
    public partial class NotesControl
    {
        private const string NoteResultPrefix = "= ";

        /// <summary>
        /// The "= ..." line for a note line, or null when the line is not a calculation
        /// (no leading ">", or nothing numeric after it, e.g. a quoted sentence).
        /// </summary>
        private static string? GetNoteResultLine(string line)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith('>'))
            {
                return null;
            }

            // "> 5 + 1 =" is accepted too.
            var expression = trimmed[1..].Trim().TrimEnd('=').Trim();
            if (!expression.Any(char.IsAsciiDigit))
            {
                return null;
            }

            return NoteResultPrefix + ExpressionEvaluator.EvaluateToText(expression);
        }

        private void NotesTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None
                || NotesTextBox.IsReadOnly || NotesTextBox.SelectionLength > 0)
            {
                return;
            }

            var text = NotesTextBox.Text;
            var caret = NotesTextBox.CaretIndex;

            // The line the caret is on, without its line break.
            var lineStart = caret == 0 ? 0 : text.LastIndexOf('\n', caret - 1) + 1;
            var lineBreak = text.IndexOf('\n', caret);
            var lineEnd = lineBreak < 0 ? text.Length : lineBreak;
            if (lineEnd > lineStart && text[lineEnd - 1] == '\r')
            {
                lineEnd--;
            }

            var result = GetNoteResultLine(text[lineStart..lineEnd]);
            if (result == null)
            {
                return; // an ordinary line: Enter behaves as usual
            }

            e.Handled = true;

            // If the next line already holds an answer, replace it.
            if (lineBreak >= 0)
            {
                var nextStart = lineBreak + 1;
                var nextBreak = text.IndexOf('\n', nextStart);
                var nextEnd = nextBreak < 0 ? text.Length : nextBreak;
                if (nextEnd > nextStart && text[nextEnd - 1] == '\r')
                {
                    nextEnd--;
                }

                if (text[nextStart..nextEnd].StartsWith(NoteResultPrefix, StringComparison.Ordinal))
                {
                    NotesTextBox.Select(nextStart, nextEnd - nextStart);
                    NotesTextBox.SelectedText = result;
                    NotesTextBox.Select(nextStart + result.Length, 0);
                    return;
                }
            }

            // Otherwise add the answer under the expression, and a fresh line to carry on typing.
            var insert = Environment.NewLine + result + Environment.NewLine;
            NotesTextBox.Select(lineEnd, 0);
            NotesTextBox.SelectedText = insert;
            NotesTextBox.Select(lineEnd + insert.Length, 0);
        }

        /// <summary>Adds or updates the answer under every ">" line (used when the notes box loses focus).</summary>
        private void RefreshNoteCalculations()
        {
            if (!notesLoaded || NotesTextBox.IsReadOnly)
            {
                return;
            }

            var text = NotesTextBox.Text;
            if (!text.Contains('>'))
            {
                return;
            }

            var newLine = text.Contains("\r\n") ? "\r\n" : text.Contains('\n') ? "\n" : Environment.NewLine;
            var lines = text.Split('\n').Select(line => line.TrimEnd('\r')).ToList();
            var output = new List<string>(lines.Count + 4);

            for (var i = 0; i < lines.Count; i++)
            {
                output.Add(lines[i]);

                var result = GetNoteResultLine(lines[i]);
                if (result == null)
                {
                    continue;
                }

                output.Add(result);

                // Skip the old answer, if there is one, now that the new one is in place.
                if (i + 1 < lines.Count && lines[i + 1].StartsWith(NoteResultPrefix, StringComparison.Ordinal))
                {
                    i++;
                }
            }

            var updated = string.Join(newLine, output);
            if (updated != text)
            {
                NotesTextBox.Text = updated;
            }
        }
    }
}
