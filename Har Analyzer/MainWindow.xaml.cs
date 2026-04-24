using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace HARFileViewer
{
    public partial class MainWindow : Window
    {
        private JObject _harData;
        private string _currentRawContent;

        public class CallEntry : INotifyPropertyChanged
        {
            private bool _isMatch;

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

            try
            {
                string fileContent = File.ReadAllText(openFileDialog.FileName);
                _harData = JObject.Parse(fileContent);

                FileNameTextBlock.Text = $"Loaded File: {Path.GetFileName(openFileDialog.FileName)}";

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

        private void ClearSearchHighlights()
        {
            foreach (var item in CallTable.Items.OfType<CallEntry>())
                item.IsMatch = false;
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
