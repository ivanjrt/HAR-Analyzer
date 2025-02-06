using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace HARFileViewer
{
    public partial class MainWindow : Window
    {
        private JObject harData;

        public class CallEntry
        {
            public string Method { get; set; }
            public string Url { get; set; }
            public string Status { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "HAR files (*.har)|*.har";
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    string fileContent = File.ReadAllText(openFileDialog.FileName);
                    harData = JObject.Parse(fileContent);

                    string fileName = Path.GetFileName(openFileDialog.FileName);
                    FileNameTextBlock.Text = $"Loaded File: {fileName}";

                    if (harData["log"] != null && harData["log"]["entries"] != null)
                    {
                        JArray entries = (JArray)harData["log"]["entries"];
                        PopulateCallTable(entries);
                        MessageBox.Show($"Loaded {entries.Count} entries from the HAR file.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("The HAR file does not contain the expected 'log.entries' structure.", "Invalid Format", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (JsonException jsonEx)
                {
                    MessageBox.Show($"Error parsing HAR file: {jsonEx.Message}", "JSON Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

            Dispatcher.Invoke(() =>
            {
                CallTable.ItemsSource = calls;
            });
        }

        private void CallTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CallTable.SelectedItem is CallEntry selectedEntry)
            {
                int index = CallTable.SelectedIndex;
                if (index >= 0 && index < harData["log"]["entries"].Count())
                {
                    string jsonContent = JsonConvert.SerializeObject(harData["log"]["entries"][index], Formatting.Indented);
                    ResponseContent.Document.Blocks.Clear();
                    ResponseContent.Document.Blocks.Add(new Paragraph(new Run(jsonContent)));
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(new TextRange(ResponseContent.Document.ContentStart, ResponseContent.Document.ContentEnd).Text);
            MessageBox.Show("Content copied to clipboard!");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseContent.Document.Blocks.Clear();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            CallTable.ItemsSource = null;
            ResponseContent.Document.Blocks.Clear();
            harData = null;
            FileNameTextBlock.Text = string.Empty;
            RawSearchTextBox.Clear();
            SearchTextBox.Clear();
            ClearSearchHighlights();
        }

        private void RawSearchButton_Click(object sender, RoutedEventArgs e)
        {
            string searchKeyword = RawSearchTextBox.Text;
            if (string.IsNullOrEmpty(searchKeyword))
            {
                MessageBox.Show("Please enter a search keyword.", "Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TextRange textRange = new TextRange(ResponseContent.Document.ContentStart, ResponseContent.Document.ContentEnd);
            string text = textRange.Text;
            int index = text.IndexOf(searchKeyword, StringComparison.OrdinalIgnoreCase);

            if (index == -1)
            {
                MessageBox.Show("No matches found.", "Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Clear previous highlights
            textRange.ClearAllProperties();

            int matchCount = 0; // Counter for the number of matches

            // Highlight all matches
            while (index != -1)
            {
                TextPointer start = textRange.Start.GetPositionAtOffset(index);
                TextPointer end = textRange.Start.GetPositionAtOffset(index + searchKeyword.Length);
                TextRange matchRange = new TextRange(start, end);
                matchRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Yellow); // Change to grey
                matchRange.ApplyPropertyValue(TextElement.ForegroundProperty, Brushes.Black);

                matchCount++; // Increment the match count
                index = text.IndexOf(searchKeyword, index + searchKeyword.Length, StringComparison.OrdinalIgnoreCase);
            }

            // Show the number of matches found
            MessageBox.Show($"Found {matchCount} match(es) for '{searchKeyword}'.", "Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string searchKeyword = SearchTextBox.Text.Trim();
            if (string.IsNullOrEmpty(searchKeyword))
            {
                MessageBox.Show("Please enter a search keyword.", "Search Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Reset previous highlighting
            ClearSearchHighlights();

            // Search and highlight matching rows
            var matchedItems = CallTable.Items
                .OfType<CallEntry>()
                .Where(entry =>
                    (entry.Method?.IndexOf(searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (entry.Url?.IndexOf(searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (entry.Status?.IndexOf(searchKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                ).ToList();

            if (matchedItems.Any())
            {
                foreach (var item in matchedItems)
                {
                    var row = CallTable.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                    if (row != null) row.Background = Brushes.DarkCyan;
                }

                MessageBox.Show($"Found {matchedItems.Count} match(es) for '{searchKeyword}'.", "Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("No matches found.", "Search Results", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearSearchHighlights()
        {
            foreach (var item in CallTable.Items)
            {
                var row = CallTable.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                if (row != null) row.Background = Brushes.Transparent;
            }
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSearchHighlights();
        }
    }
}