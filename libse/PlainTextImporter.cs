﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nikse.SubtitleEdit.Core
{
    public class PlainTextImporter
    {
        private readonly bool _splitAtBlankLines;
        private readonly bool _removeLinesWithoutLetters;
        private readonly int _numberOfLines;
        private readonly string _endChars;
        private readonly int _singleLineMaxLength;
        private readonly string _language;

        public PlainTextImporter(bool splitAtBlankLines, bool removeLinesWithoutLetters, int numberOfLines, string endChars, int singleLineMaxLength, string language)
        {
            _splitAtBlankLines = splitAtBlankLines;
            _removeLinesWithoutLetters = removeLinesWithoutLetters;
            _numberOfLines = numberOfLines;
            if (endChars == null)
                endChars = string.Empty;
            _endChars = endChars;
            _singleLineMaxLength = singleLineMaxLength;
            _language = language;
        }

        public List<string> ImportAutoSplit(IEnumerable<string> textLines)
        {
            var sb = new StringBuilder();
            foreach (string line in textLines)
            {
                var s = line.Replace("\0", string.Empty);
                if (string.IsNullOrWhiteSpace(s) && _splitAtBlankLines)
                {
                    sb.Append('\0');
                }
                else if (!ContainsLetters(s.Trim()))
                {
                    if (!_removeLinesWithoutLetters)
                        sb.AppendLine(s);
                }
                else
                {
                    sb.AppendLine(s);
                }
            }
            string text = sb.ToString().Replace(Environment.NewLine, " ");


            var list = new List<string>();
            var untilLastSpace = new StringBuilder();
            var fromLastSpace = new StringBuilder();
            var maxLength = _singleLineMaxLength * _numberOfLines;
            bool oneLineOnly = _numberOfLines == 1;
            var hardSplitIndices = new List<int>();
            for (var index = 0; index < text.Length; index++)
            {
                var ch = text[index];
                if (ch == '\0')
                {
                    if (list.Count > 0)
                    {
                        hardSplitIndices.Add(list.Count);
                    }

                    var nextWord = AutoSplitAddLine(oneLineOnly, list, untilLastSpace.ToString() + fromLastSpace, text, index, hardSplitIndices);
                    untilLastSpace.Clear();
                    fromLastSpace.Clear();
                    untilLastSpace.Append(nextWord);
                }
                else if (ch == ' ')
                {
                    if (untilLastSpace.Length + fromLastSpace.Length > maxLength)
                    {
                        var nextWord = AutoSplitAddLine(oneLineOnly, list, untilLastSpace.ToString(), text, index, hardSplitIndices);
                        untilLastSpace.Clear();
                        untilLastSpace.Append(nextWord);
                    }
                    untilLastSpace.Append(fromLastSpace);
                    if (untilLastSpace.Length > 0)
                    {
                        untilLastSpace.Append(ch);
                    }

                    fromLastSpace.Clear();
                }
                else if (_endChars.Contains(ch) && index < text.Length - 2 && text[index + 1] == ' ')
                {
                    fromLastSpace.Append(ch);

                    if (untilLastSpace.Length + fromLastSpace.Length > maxLength)
                    {
                        var nextWord = AutoSplitAddLine(oneLineOnly, list, untilLastSpace.ToString(), text, index, hardSplitIndices);
                        untilLastSpace.Clear();
                        untilLastSpace.Append(nextWord);
                    }
                    else
                    {
                        var nextWord = AutoSplitAddLine(oneLineOnly, list, untilLastSpace.ToString() + fromLastSpace, text, index, hardSplitIndices);
                        untilLastSpace.Clear();
                        untilLastSpace.Append(nextWord);
                        fromLastSpace.Clear();
                    }
                }
                else
                {
                    fromLastSpace.Append(ch);
                }
            }
            AutoSplitAddLine(oneLineOnly, list, untilLastSpace.ToString() + fromLastSpace, text, text.Length, hardSplitIndices);
            return list;
        }

        private string AutoSplitAddLine(bool oneLineOnly, List<string> list, string text, string allText, int allIndex, List<int> hardSplitIndices)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            if (oneLineOnly)
            {
                list.Add(Utilities.UnbreakLine(text.Trim()));
                return string.Empty;
            }

            var line = Utilities.AutoBreakLine(text.Trim(), _singleLineMaxLength, Configuration.Settings.Tools.MergeLinesShorterThan, _language);
            if (ExceedsMax(line))
            {
                var lastWords = string.Empty;
                for (int i = 0; i < 3; i++)
                {
                    int lastIndexOfSpace = line.LastIndexOf(' ');
                    if (lastIndexOfSpace > 0 && CanLastWordBeMovedToNext(line, allText, allIndex))
                    {
                        lastWords = line.Substring(lastIndexOfSpace).Trim() + " " + lastWords;
                        line = line.Substring(0, lastIndexOfSpace).Trim();
                        line = Utilities.AutoBreakLine(line, _singleLineMaxLength, Configuration.Settings.Tools.MergeLinesShorterThan, _language);
                        if (!ExceedsMax(line))
                        {
                            list.Add(line);
                            return lastWords.Trim() + " "; // use this word in next paragraph
                        }
                    }
                }
                foreach (var l in SplitToThreeOrFourLines(text))
                {
                    list.Add(l);
                }
            }
            else
            {
                if (list.Count > 0 &&
                    line.Length < _singleLineMaxLength / 2.5 &&
                    !hardSplitIndices.Contains(list.Count-1) &&
                    CanLastWordBeMovedToNext(list[list.Count - 1], allText, allIndex))
                {
                    var three = SplitToThree(Utilities.UnbreakLine(list[list.Count - 1] + " " + line));
                    if (three.Count == 3)
                    {
                        list.RemoveAt(list.Count - 1);
                        var firstLine = Utilities.AutoBreakLine(three[0] + " " + three[1], _singleLineMaxLength, Configuration.Settings.Tools.MergeLinesShorterThan, _language);
                        list.Add(firstLine);
                        list.Add(three[2]);
                        return string.Empty;
                    }
                }
                list.Add(line);
            }
            return string.Empty;
        }

        private bool ExceedsMax(string line)
        {
            foreach (var s in line.SplitToLines())
            {
                if (s.Length > _singleLineMaxLength)
                    return true;
            }
            return false;
        }

        private bool CanLastWordBeMovedToNext(string line, string allText, int allIndex)
        {
            if (allIndex == allText.Length || _endChars.Contains(line[line.Length - 1]))
                return false;
            return true;
        }

        private List<string> SplitToThreeOrFourLines(string text)
        {
            text = Utilities.UnbreakLine(text);
            var three = SplitToThree(text);
            var four = SplitToFour(text);
            if (three.Count == 3)
            {
                return three[0].Length < _singleLineMaxLength &&
                       three[1].Length < _singleLineMaxLength &&
                       three[2].Length < _singleLineMaxLength
                    ? three
                    : new List<string> { text };
            }
            if (four.Count == 4)
            {
                return four;
            }
            return new List<string> { text };
        }

        private List<string> SplitToFour(string text)
        {
            var lines = Utilities.AutoBreakLine(text.Trim(), _singleLineMaxLength, Configuration.Settings.Tools.MergeLinesShorterThan, _language).SplitToLines();
            var list = new List<string>();
            foreach (var line in lines)
            {
                list.Add(Utilities.AutoBreakLine(line, _singleLineMaxLength, Configuration.Settings.Tools.MergeLinesShorterThan, _language));
            }
            return list;
        }

        internal class SplitListItem
        {
            internal List<string> Lines { get; set; }

            internal double DiffFromAverage(double avg)
            {
                var dif = 0.0;
                foreach (var line in Lines)
                {
                    dif += Math.Abs(avg - line.Length);
                }
                return dif;
            }
        }

        private List<string> SplitToThree(string text)
        {
            text = text.Trim();
            var results = new List<SplitListItem>();
            for (int maxLength = _singleLineMaxLength; maxLength > 5; maxLength--)
            {
                var list = new List<string>();
                var lastIndexOfSpace = -1;
                int start = 0;
                for (int i = 0; i < text.Length; i++)
                {
                    var ch = text[i];
                    if (ch == ' ')
                    {
                        if (i - start > maxLength && lastIndexOfSpace > start)
                        {
                            var line = text.Substring(start, lastIndexOfSpace - start);
                            list.Add(line.Trim());
                            start = lastIndexOfSpace + 1;
                        }
                        lastIndexOfSpace = i;
                    }
                }
                var lastLine = text.Substring(start);
                list.Add(lastLine.Trim());
                if (list.Count > 3)
                    break;
                results.Add(new SplitListItem { Lines = list });
            }

            var avg = text.Length / 3.0;
            var best = results.Where(p => p.Lines.Count == 3).OrderBy(p => p.DiffFromAverage(avg)).FirstOrDefault();
            if (best == null)
                return new List<string> { text };
            return best.Lines;
        }

        public static bool ContainsLetters(string line)
        {
            if (string.IsNullOrWhiteSpace(line.Replace("0", string.Empty).Replace("1", string.Empty).Replace("2", string.Empty).Replace("3", string.Empty).Replace("4", string.Empty).Replace("5", string.Empty).Replace("6", string.Empty)
                .Replace("7", string.Empty).Replace("8", string.Empty).Replace("9", string.Empty).RemoveChar(':').RemoveChar('.').RemoveChar(',').
                RemoveChar('-').RemoveChar('>').RemoveChar('/')))
            {
                return false;
            }

            const string expectedChars = "\r\n\t .?\0";
            foreach (char ch in line)
            {
                if (!expectedChars.Contains(ch))
                    return true;
            }
            return false;
        }

    }
}