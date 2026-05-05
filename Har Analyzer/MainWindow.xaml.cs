using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Har_Analyzer
{
    public partial class MainWindow : Window
    {
        private JObject _harData;
        private string _currentRawContent;

        public class CallEntry : INotifyPropertyChanged
        {
            private bool _isMatch;
            private int _matchCount;

            public string Method { get; set; }
            public string Url { get; set; }
            public string Status { get; set; }

            public bool IsMatch
            {
                get => _isMatch;
                set
                {
                    if (_isMatch != value)
                    {
                        _isMatch = value;
                        OnPropertyChanged();
                    }
                }
            }

            /// <summary>
            /// How many times the current deep-search keyword appears in this
            /// entry's full JSON. 0 when no deep search is active or no hits.
            /// </summary>
            public int MatchCount
            {
                get => _matchCount;
                set
                {
                    if (_matchCount != value)
                    {
                        _matchCount = value;
                        OnPropertyChanged();
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "HAR files (*.har)|*.har"
            };

            if (openFileDialog.ShowDialog() != true)
                return;

            LoadHarFile(openFileDialog.FileName);
        }

        /// <summary>
        /// Allows drag-over to show a copy cursor only for valid .har files.
        /// </summary>
        private void BrowseButton_DragOver(object sender, DragEventArgs e)
        {
            bool valid = IsHarDrop(e.Data);
            e.Effects = valid ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        /// <summary>
        /// Loads the dropped .har file (first one if multiple are dropped).
        /// </summary>
        private void BrowseButton_Drop(object sender, DragEventArgs e)
        {
            if (!IsHarDrop(e.Data))
                return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0)
                return;

            LoadHarFile(files[0]);
        }

        /// <summary>
        /// True when the drag payload carries at least one .har file.
        /// </summary>
        private static bool IsHarDrop(IDataObject data)
        {
            if (!data.GetDataPresent(DataFormats.FileDrop))
                return false;

            var files = (string[])data.GetData(DataFormats.FileDrop);
            return files != null && files.Length > 0 &&
                   files[0].EndsWith(".har", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Shared load path for browse and drag-and-drop. Parses the HAR,
        /// populates the call table, and reports success/failure.
        /// </summary>
        private void LoadHarFile(string filePath)
        {
            try
            {
                string fileContent = File.ReadAllText(filePath);
                _harData = JObject.Parse(fileContent);

                FileNameTextBlock.Text = $"Loaded File: {Path.GetFileName(filePath)}";

                if (_harData["log"]?["entries"] is JArray entries)
                {
                    PopulateCallTable(entries);
                    MessageBox.Show($"Loaded {entries.Count} entries from the HAR file.",
                        "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("The HAR file does not contain the expected 'log.entries' structure.",
                        "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (JsonException jsonEx)
            {
                MessageBox.Show($"Error parsing HAR file: {jsonEx.Message}",
                    "JSON Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading file: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PopulateCallTable(JArray entries)
        {
            var calls = new List<CallEntry>();
            foreach (var entry in entries)
            {
                calls.Add(new CallEntry
                {
                    Method = entry["request"]?["method"]?.ToString(),
                    Url = entry["request"]?["url"]?.ToString(),
                    Status = entry["response"]?["status"]?.ToString()
                });
            }

            CallTable.ItemsSource = calls;
        }

        private void CallTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(CallTable.SelectedItem is CallEntry) || _harData == null)
                return;

            int index = CallTable.SelectedIndex;
            var entries = _harData["log"]?["entries"];
            if (entries == null || index < 0 || index >= entries.Count())
                return;

            _currentRawContent = JsonConvert.SerializeObject(entries[index], Formatting.Indented);
            DisplayRawContent(_currentRawContent);
        }

        /// <summary>
        /// Displays raw content in the RichTextBox, optionally highlighting all matches of a keyword.
        /// Rebuilds the document using Runs to guarantee correct highlight positions.
        /// </summary>
        private void DisplayRawContent(string content, string highlightKeyword = null)
        {
            ResponseContent.Document.Blocks.Clear();

            var paragraph = new Paragraph { Margin = new Thickness(0) };

            if (string.IsNullOrEmpty(highlightKeyword))
            {
                paragraph.Inlines.Add(new Run(content));
                ResponseContent.Document.Blocks.Add(paragraph);
                return;
            }

            // Walk through the content, splitting into plain Runs and highlighted Runs
            int currentPosition = 0;
            int matchCount = 0;

            while (currentPosition < content.Length)
            {
                int matchIndex = content.IndexOf(highlightKeyword, currentPosition, StringComparison.OrdinalIgnoreCase);

                if (matchIndex == -1)
                {
                    // No more matches — add remaining text
                    paragraph.Inlines.Add(new Run(content.Substring(currentPosition)));
                    break;
                }

                // Text before the match
                if (matchIndex > currentPosition)
                {
                    paragraph.Inlines.Add(new Run(content.Substring(currentPosition, matchIndex - currentPosition)));
                }

                // Highlighted match
                paragraph.Inlines.Add(new Run(content.Substring(matchIndex, highlightKeyword.Length))
                {
                    Background = Brushes.Yellow,
                    Foreground = Brushes.Black
                });

                currentPosition = matchIndex + highlightKeyword.Length;
                matchCount++;
            }

            ResponseContent.Document.Blocks.Add(paragraph);

            MessageBox.Show(matchCount > 0
                ? $"Found {matchCount} match(es) for '{highlightKeyword}'."
                : "No matches found.",
                "Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            string text = new TextRange(ResponseContent.Document.ContentStart, ResponseContent.Document.ContentEnd).Text;
            Clipboard.SetText(text);
            MessageBox.Show("Content copied to clipboard!");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseContent.Document.Blocks.Clear();
            _currentRawContent = null;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://github.com/ivanjrt/HAR-Analyzer",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unable to open help page: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            CallTable.ItemsSource = null;
            ResponseContent.Document.Blocks.Clear();
            _harData = null;
            _currentRawContent = null;
            FileNameTextBlock.Text = string.Empty;
            RawSearchTextBox.Clear();
            SearchTextBox.Clear();
            ClearSearchHighlights();
        }

        private void RawSearchButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = RawSearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("Please enter a search keyword.",
                    "Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_currentRawContent))
            {
                MessageBox.Show("No content to search. Select an entry first.",
                    "Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DisplayRawContent(_currentRawContent, keyword);
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("Please enter a search keyword.",
                    "Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ClearSearchHighlights();

            var matchedItems = CallTable.Items
                .OfType<CallEntry>()
                .Where(entry =>
                    entry.Method?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Url?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    entry.Status?.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            foreach (var item in matchedItems)
                item.IsMatch = true;

            MessageBox.Show(matchedItems.Any()
                ? $"Found {matchedItems.Count} match(es) for '{keyword}'."
                : "No matches found.",
                "Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Deep search: scans the FULL JSON of every loaded entry (request,
        /// response, headers, body — everything) for the keyword and highlights
        /// matching rows. The per-entry hit count is shown in the Matches column
        /// so you can jump straight to the most relevant call instead of clicking
        /// through entries one by one.
        /// </summary>
        private void DeepSearchButton_Click(object sender, RoutedEventArgs e)
        {
            string keyword = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(keyword))
            {
                MessageBox.Show("Please enter a search keyword.",
                    "Deep Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_harData == null)
            {
                MessageBox.Show("Load a HAR file first.",
                    "Deep Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var entries = _harData["log"]?["entries"];
            if (entries == null)
            {
                MessageBox.Show("No entries to search.",
                    "Deep Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var calls = CallTable.Items.OfType<CallEntry>().ToList();
            int matchedEntries = 0;
            int totalMatches = 0;
            int idx = 0;

            foreach (var entry in entries)
            {
                int count = 0;
                if (idx < calls.Count)
                {
                    // Full serialized JSON of this entry: method, url, status,
                    // request/response headers and the response body — the lot.
                    string fullJson = JsonConvert.SerializeObject(entry, Formatting.None);
                    count = CountOccurrences(fullJson, keyword, StringComparison.OrdinalIgnoreCase);

                    calls[idx].MatchCount = count;
                    calls[idx].IsMatch = count > 0;
                }

                if (count > 0)
                {
                    matchedEntries++;
                    totalMatches += count;
                }
                idx++;
            }

            MessageBox.Show(matchedEntries > 0
                ? $"Found '{keyword}' in {matchedEntries} of {calls.Count} entries ({totalMatches} total match(es))."
                : $"No matches for '{keyword}' across {calls.Count} entries.",
                "Deep Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Counts non-overlapping occurrences of <paramref name="term"/> in
        /// <paramref name="source"/> using the given comparison.
        /// </summary>
        private static int CountOccurrences(string source, string term, StringComparison comparison)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(term))
                return 0;

            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(term, index, comparison)) >= 0)
            {
                count++;
                index += term.Length;
            }
            return count;
        }

        private void ClearSearchHighlights()
        {
            foreach (var item in CallTable.Items.OfType<CallEntry>())
            {
                item.IsMatch = false;
                item.MatchCount = 0;
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSearchHighlights();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SearchButton_Click(sender, e);
        }

        private void RawSearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                RawSearchButton_Click(sender, e);
        }
    }
}
