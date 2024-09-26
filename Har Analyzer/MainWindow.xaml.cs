using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HARFileViewer
{
    public partial class MainWindow : Window
    {
        private JObject harData;

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
                    ResponseContent.Text = JsonConvert.SerializeObject(harData["log"]["entries"][index], Formatting.Indented);
                }
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ResponseContent.Text);
            MessageBox.Show("Content copied to clipboard!");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseContent.Clear();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            CallTable.ItemsSource = null;
            ResponseContent.Clear();
            harData = null;
            FileNameTextBlock.Text = string.Empty;
        }
    }

    public class CallEntry
    {
        public string Method { get; set; }
        public string Url { get; set; }
        public string Status { get; set; }
    }
}