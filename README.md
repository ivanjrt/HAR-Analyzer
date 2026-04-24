# HAR Analyzer
This Repo will to read a HAR trace and display its entries in a RAW format. <br/>
if you have other ideas to add please let me know <br/>
![image](https://github.com/user-attachments/assets/84816499-d6d4-4848-8593-b1787f5e16b8)<br/>

# HAR Analyzer 🔍

A lightweight Windows desktop app for poking around inside HAR (HTTP Archive) files. You know those `.har` files you export from browser DevTools? Yeah, this lets you actually *read* them without drowning in raw JSON. Built with WPF (C# / XAML) on .NET Framework 4.7.2.

## ✨ Features

- 📂 **Browse & load** any `.har` file — Chrome, Firefox, Edge, whatever browser you're using
- 📋 **Table view** of all network requests at a glance — Method, URL, and HTTP Status
- 📄 **Raw JSON viewer** — click any row to dig into the full entry (headers, response body, timings, the works)
- 🔎 **Search the table** — type a keyword and it'll highlight matching rows across Method, URL, or Status
- 🔦 **Search raw content** — find and highlight text right inside the selected entry's JSON
- 📑 **Copy to clipboard** — one click and it's yours
- 🌙 **Dark theme** because light mode is a crime

## ⚙️ How It Works

```
 📁 You pick a .har file
         |
         v
 📖 File is read into memory (nothing saved anywhere)
         |
         v
 🧩 Newtonsoft.Json parses it into a JObject
         |
         v
 📊 entries[] populates the DataGrid
    (Method | URL | Status)
         |
         v
 👆 You click a row → that entry gets serialized
    to pretty-printed JSON and shows up on the right
         |
         v
 🔍 Search & highlight in the table
    or inside the raw JSON — your call
```

**Under the hood:**

- The HAR file is parsed with **Newtonsoft.Json** and kept in memory as a `JObject`
- The left panel `DataGrid` binds to a `List<CallEntry>` built from `log.entries[]`
- Clicking a row serializes that entry to indented JSON and dumps it into the right-side `RichTextBox`
- Raw content search rebuilds the document with `Run` elements so highlights land exactly where they should
- Table search uses `INotifyPropertyChanged` + `DataTrigger` — so your highlights don't vanish when you scroll

## 🔒 Security & Privacy

This was built with a "do no harm" mindset:

- 🚫 **Nothing is written to disk.** The app only *reads* the `.har` file you hand it
- 🌐 **No network calls, period.** Everything stays on your machine — no telemetry, no analytics, no phone-home nonsense
- 🤐 **No data leaves the app.** The clipboard copy is the only way out, and *you* decide when to use it
- 🧹 **No config or cache files.** Close the app and it's all gone from memory

## 💻 Requirements

- 🪟 Windows 7 or later
- 📦 [.NET Framework 4.7.2](https://dotnet.microsoft.com/download/dotnet-framework/net472) (already on Windows 10 1803+)

## 🛠️ Running from Source

1. Open `Har Analyzer.sln` in Visual Studio 2017+
2. Restore NuGet packages (Newtonsoft.Json 13.0.3)
3. Build & run — that's it

## ⚠️ SmartScreen Warning

Not gonna lie — since this app isn't signed with a paid code-signing certificate, Windows is gonna complain on first run. Totally normal. Here's how to get past it:

1. Click **More info**
2. Click **Run anyway**

Yeah it's annoying, but code signing certs aren't free 😅

## 📜 License

See [LICENSE.txt](LICENSE.txt).

---

*Got ideas? Found a bug? Feel free to open an issue or a PR — contributions are always welcome! 🤝*


* You are free to either give me more ideas or collaborate :)

* Due to the fact that I don't have money to buy my own _Code Signing Certificate_ you will 💯 receive this the first time you run the app<br/>
![image](https://github.com/ivanjrt/SCCM-Capabilities-Codes-Analyzer/assets/44326428/745209e0-f13e-4c80-bd19-b893dc000c27)<br/>
![image](https://github.com/ivanjrt/SearchFilesTools/assets/44326428/381bb43a-4e87-4db2-b0a4-ce8f7e536062)<br/>
**Fix**:<br/>
**Click More info > Run Anyway<br/>

