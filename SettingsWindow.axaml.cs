using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using OperaSuprema.Core;
using System;
using System.Collections.Generic;

namespace OperaSuprema.GUI
{
    public partial class SettingsWindow : Window
    {
        private ConfigManager _configManager = null!;
        private AppConfig _tempConfig = null!;

        private Dictionary<TextBox, ModelDefinition> _textBoxToModelMap = new();

        public SettingsWindow()
        {
            InitializeComponent();
        }

        public SettingsWindow(ConfigManager configManager) : this()
        {
            _configManager = configManager;
            LoadDataIntoUI(System.Text.Json.JsonSerializer.Serialize(_configManager.CurrentConfig));
        }

        private void LoadDataIntoUI(string jsonSource)
        {
            _tempConfig = System.Text.Json.JsonSerializer.Deserialize<AppConfig>(jsonSource)!;

            // Aggiorna il Toggle Hot-Swap
            var chkHotSwap = this.FindControl<CheckBox>("ChkHotSwap");
            if (chkHotSwap != null)
            {
                chkHotSwap.IsChecked = _tempConfig.HotSwapEnabled;
                chkHotSwap.IsCheckedChanged += (s, e) => _tempConfig.HotSwapEnabled = chkHotSwap.IsChecked == true;
            }

	    // --- NUOVO: CARICA IL PERCORSO LLAMA.CPP NELLA UI ---
            var llamaTextBox = this.FindControl<TextBox>("LlamaPathTextBox");
            if (llamaTextBox != null)
            {
                llamaTextBox.Text = _tempConfig.LlamaServerPath;
            }

            var btnBrowseLlama = this.FindControl<Button>("BtnBrowseLlama");
            if (btnBrowseLlama != null && llamaTextBox != null)
            {
                btnBrowseLlama.Click += async (s, e) =>
                {
                    // Chiediamo di selezionare una CARTELLA, non un file
                    var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                    {
                        Title = "Seleziona la cartella di llama.cpp",
                        AllowMultiple = false
                    });

                    if (folders.Count > 0)
                    {
                        string selectedFolder = folders[0].Path.LocalPath;
                        string foundExecutable = "";

                        // Cerca l'eseguibile (llama-server o llama-server.exe) anche nelle sottocartelle
                        try
                        {
                            var files = System.IO.Directory.GetFiles(selectedFolder, "*server*", System.IO.SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                string nomeFile = System.IO.Path.GetFileName(file).ToLower();
                                if (nomeFile == "llama-server" || nomeFile == "llama-server.exe")
                                {
                                    foundExecutable = file;
                                    break;
                                }
                            }
                        }
                        catch 
                        {
                            // Ignora errori se l'utente seleziona cartelle di sistema protette
                        }

                        if (!string.IsNullOrEmpty(foundExecutable))
                        {
                            llamaTextBox.Text = foundExecutable;
                            _tempConfig.LlamaServerPath = foundExecutable;
                        }
                        else
                        {
                            // Se non lo trova, avvisa l'utente
                            llamaTextBox.Text = "ERRORE: Eseguibile non trovato in questa cartella!";
                            _tempConfig.LlamaServerPath = "";
                        }
                    }
                };
            }

	    // --- NUOVO: CARICA IL TOKEN TELEGRAM NELLA UI ---
            var tokenTextBox = this.FindControl<TextBox>("TelegramTokenTextBox");
            if (tokenTextBox != null)
            {
                tokenTextBox.Text = _tempConfig.TelegramToken;
            }

            // Pulisci i pannelli prima di ridisegnare (utile per il ripristino default)
            var panelHacker = this.FindControl<StackPanel>("PanelHacker");
            var panelAccademia = this.FindControl<StackPanel>("PanelAccademia");
            panelHacker?.Children.Clear();
            panelAccademia?.Children.Clear();
            _textBoxToModelMap.Clear();

            BuildDynamicUI("HACKER", panelHacker);
            BuildDynamicUI("ACCADEMIA", panelAccademia);
        }

        private void BuildDynamicUI(string modeKey, StackPanel? panel)
        {
            if (panel == null || !_tempConfig.Modes.ContainsKey(modeKey)) return;

            foreach (var model in _tempConfig.Modes[modeKey])
            {
                var border = new Border { Background = Brush.Parse("#18181A"), Padding = new Avalonia.Thickness(10), CornerRadius = new Avalonia.CornerRadius(8), BorderBrush = Brush.Parse("#3F3F46"), BorderThickness = new Avalonia.Thickness(1) };
                var mainStack = new StackPanel { Spacing = 5 };

                var label = new TextBlock { Text = $"Modulo: {model.Id} (Porta {model.Port})", Foreground = Brushes.LightGray, FontWeight = FontWeight.Bold };
                mainStack.Children.Add(label);
                
                // RIGA 1: File
                var gridFile = new Grid { ColumnDefinitions = new ColumnDefinitions("*, Auto") };
                var textBox = new TextBox { Text = model.FileName, IsReadOnly = true, Background = Brush.Parse("#252526") };
                Grid.SetColumn(textBox, 0);
                
                var browseBtn = new Button { Content = "📁 Sfoglia...", Margin = new Avalonia.Thickness(10,0,0,0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
                Grid.SetColumn(browseBtn, 1);
                _textBoxToModelMap[textBox] = model;

                browseBtn.Click += async (s, e) => 
                {
                    var startFolder = await StorageProvider.TryGetFolderFromPathAsync(new Uri($"file://{_tempConfig.StoragePath}"));
                    var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions 
                    { 
                        Title = $"Seleziona il file .gguf per {model.Id}", AllowMultiple = false, SuggestedStartLocation = startFolder
                    });
                    if (files.Count > 0)
                    {
                        string selectedFile = files[0].Name;
                        textBox.Text = selectedFile; 
                        _textBoxToModelMap[textBox].FileName = selectedFile; 
                    }
                };
                gridFile.Children.Add(textBox); gridFile.Children.Add(browseBtn);
                mainStack.Children.Add(gridFile);

                // RIGA 2: Parametri di Ottimizzazione
                var paramsPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 15, Margin = new Avalonia.Thickness(0, 5, 0, 0) };
                
                paramsPanel.Children.Add(new TextBlock { Text = "Contesto:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Foreground = Brushes.Gray });
                var ctxBox = new TextBox { Text = model.ContextSize.ToString(), Width = 80, Background = Brush.Parse("#252526") };
                ctxBox.TextChanged += (s, e) => { if (int.TryParse(ctxBox.Text, out int val)) model.ContextSize = val; };
                paramsPanel.Children.Add(ctxBox);

                paramsPanel.Children.Add(new TextBlock { Text = "Cache:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Foreground = Brushes.Gray });
                var kvCombo = new ComboBox { Width = 90, Background = Brush.Parse("#252526") };
                kvCombo.Items.Add("fp16"); kvCombo.Items.Add("q8_0"); kvCombo.Items.Add("q4_0");
                kvCombo.SelectedItem = model.KvCacheType;
                kvCombo.SelectionChanged += (s, e) => { if (kvCombo.SelectedItem != null) model.KvCacheType = kvCombo.SelectedItem.ToString()!; };
                paramsPanel.Children.Add(kvCombo);

                var faCheck = new CheckBox { Content = "Attiva FlashAttention", IsChecked = model.UseFlashAttention, Foreground = Brushes.SpringGreen };
                faCheck.IsCheckedChanged += (s, e) => model.UseFlashAttention = faCheck.IsChecked == true;
                paramsPanel.Children.Add(faCheck);

                mainStack.Children.Add(paramsPanel);

		// --- NUOVO: SLIDER VRAM / RAM ---
                var sliderPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
                
                var sliderLabel = new TextBlock { Text = "Offload su VRAM:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Foreground = Brushes.Gray, FontWeight = FontWeight.Bold };
                sliderPanel.Children.Add(sliderLabel);

                var offloadSlider = new Slider 
                { 
                    Minimum = 0, Maximum = 100, 
                    Value = model.GpuOffload, 
                    Width = 200, 
                    TickFrequency = 1, 
                    IsSnapToTickEnabled = true 
                };

                var valText = new TextBlock { Text = $"{model.GpuOffload}%", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Width = 50, Foreground = Brushes.SpringGreen, FontWeight = FontWeight.Bold };
                
                // Aggiorna il testo e salva il valore in tempo reale mentre muovi la levetta
                offloadSlider.PropertyChanged += (s, e) => 
                {
                    if (e.Property == Slider.ValueProperty)
                    {
                        int val = (int)offloadSlider.Value;
                        model.GpuOffload = val;
                        valText.Text = $"{val}%";
                        
                        // Cambia colore per dare feedback visivo (Rosso=RAM, Verde=VRAM)
                        valText.Foreground = val < 50 ? Brushes.OrangeRed : Brushes.SpringGreen;
                    }
                };

                sliderPanel.Children.Add(offloadSlider);
                sliderPanel.Children.Add(valText);
                
                // Aggiungiamo il pannello dello slider sotto al pannello dei parametri
                mainStack.Children.Add(sliderPanel);

                border.Child = mainStack;
                panel.Children.Add(border);
            }
        }

        private void OnRestoreDefaultsClicked(object? sender, RoutedEventArgs e)
        {
            // Genera il config di base pulito e ricarica l'interfaccia
            var defaultConfig = _configManager.GenerateDefaultConfig();
            string jsonSource = System.Text.Json.JsonSerializer.Serialize(defaultConfig);
            LoadDataIntoUI(jsonSource);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            // --- NUOVO: SALVA IL TOKEN TELEGRAM NEL JSON ---
            var tokenTextBox = this.FindControl<TextBox>("TelegramTokenTextBox");
            if (tokenTextBox != null)
            {
                _tempConfig.TelegramToken = tokenTextBox.Text?.Trim() ?? "";
            }

            _configManager.SaveConfig(_tempConfig);
            Close();
        }
    }
}