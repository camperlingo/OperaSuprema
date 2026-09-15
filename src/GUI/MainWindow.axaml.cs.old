using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Input.Platform;
using OperaSuprema.Core;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.ReplyMarkups;
using Avalonia.Media.Imaging;

namespace OperaSuprema.GUI
{
    public partial class MainWindow : Window
    {
        private WorkspaceManager? _workspaceManager;
        private ReactiveWorkspace? _reactiveWorkspace;
        private string? _currentImagePath;
        private string? _currentWorkspacePath;

	private SessionManager _sessionManager = new SessionManager();
	private ChatSession _currentSession = new ChatSession();
        
        // Cronologia e Flag Vocale
        private HashSet<string> _loadedProjectsHistory = new HashSet<string>();
        private bool _isVoiceSession = false;
	private readonly ModelContainerManager _containerManager = new ModelContainerManager();
        private TelegramBotClient? _botClient;
        private CancellationTokenSource _botCts = new();
        private long _lastTelegramChatId = 0; 
        private string _lastTelegramRequest = ""; // Memoria a breve termine per la Macchina a Stati

	private readonly ConfigManager _configManager = new ConfigManager();

        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly List<Dictionary<string, string>> _chatHistory = new();
        private readonly List<Dictionary<string, object>> _jakHistory = new();
        private readonly VectorMemoryManager _vectorMemory = new VectorMemoryManager();
	private readonly AutonomousCrawler _crawler;
        private bool _isRecording = false;
        private System.Diagnostics.Process? _audioProcess;
        private readonly string _audioTempPath = Path.Combine(Path.GetTempPath(), "opera_dictation.wav");

        public MainWindow()
        {
            InitializeComponent();

	    // Aggancio bottone Blueprint
            var btnBlueprint = this.FindControl<Button>("OpenBlueprintButton");
            if (btnBlueprint != null)
            {
                btnBlueprint.Click += async (s, e) => await OpenOrInitializeBlueprintAsync();
            }

	    _httpClient.Timeout = TimeSpan.FromMinutes(5);

            _crawler = new AutonomousCrawler(_vectorMemory);
            
            // Memoria dell'Architetto (Opera Suprema)
            _chatHistory.Add(new Dictionary<string, string> 
            { 
                { "role", "system" }, 
                { "content", GetDynamicSystemPrompt() } 
            });

            // Memoria dell'Assistente (jak L0)
            _jakHistory.Add(new Dictionary<string, object> 
            { 
                { "role", "system" }, 
                { "content", "Sei jak, l'Assistente Universale L0 di Emanuele. Sei empatico, diretto e capace di rispondere a domande generiche, riassumere documenti o dare consigli. Non sei limitato alla programmazione." } 
            });

	    var closeProjectBtn = this.FindControl<Button>("CloseProjectButton");
            if (closeProjectBtn != null) closeProjectBtn.Click += OnCloseProjectButtonClicked;

            var browseButton = this.FindControl<Button>("BrowseButton");
            if (browseButton != null) browseButton.Click += OnBrowseButtonClicked;

            var indexButton = this.FindControl<Button>("IndexButton");
            if (indexButton != null) indexButton.Click += OnIndexButtonClicked;

            var attachButton = this.FindControl<Button>("AttachImageButton");
            if (attachButton != null) attachButton.Click += OnAttachButtonClicked;

            var removeButton = this.FindControl<Button>("RemoveImageButton");
            if (removeButton != null) removeButton.Click += OnRemoveImageButtonClicked;

            var sendButton = this.FindControl<Button>("SendButton");
            if (sendButton != null) sendButton.Click += OnSendButtonClicked;

            // NUOVI BOTTONI DELLO SPLASH SCREEN:
            var btnIde = this.FindControl<Button>("BootIdeModeButton");
            if (btnIde != null) btnIde.Click += async (s, e) => await BootSystemModeAsync("ACCADEMIA");

            var btnHacker = this.FindControl<Button>("BootHackerModeButton");
            if (btnHacker != null) btnHacker.Click += async (s, e) => await BootSystemModeAsync("HACKER");

            var micButton = this.FindControl<Button>("MicButton");
            if (micButton != null) micButton.Click += OnMicButtonClicked;

	    var swapBtn = this.FindControl<Button>("SwapInfrastructureButton");
            if (swapBtn != null) swapBtn.Click += OnSwapInfrastructureClicked;

	    var menuSettings = this.FindControl<MenuItem>("MenuSettings");
            if (menuSettings != null) menuSettings.Click += OnMenuSettingsClicked;

            var menuExit = this.FindControl<MenuItem>("MenuExit");
            if (menuExit != null) menuExit.Click += (s, e) => this.Close();

            var projectSelector = this.FindControl<ComboBox>("ProjectSelector");
            if (projectSelector != null) projectSelector.SelectionChanged += OnProjectSelectionChanged;

            // --- AGGIUNGI QUI L'ASCOLTO DELLA TASTIERA E DEL MOUSE ---
            var inputTextBox = this.FindControl<TextBox>("UserInputTextBox");
            if (inputTextBox != null) 
            {
                // Ascolta l'Invio
                inputTextBox.AddHandler(InputElement.KeyDownEvent, OnUserInputTextBoxKeyDown, RoutingStrategies.Tunnel);
                
                // NUOVO: Ascolta qualsiasi evento "Incolla" (Tastiera, Tasto Destro, Shell)
                inputTextBox.PastingFromClipboard += OnTextBoxPastingFromClipboard;
            }

            // --- AVVIO TELEGRAM BOT ---
            string botToken = _configManager.CurrentConfig.TelegramToken;
            if (!string.IsNullOrWhiteSpace(botToken))
            {
                try 
                {
                    _botClient = new TelegramBotClient(botToken);
                    _botClient.StartReceiving(
                        HandleTelegramUpdateAsync, 
                        HandleTelegramErrorAsync, 
                        new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() }, 
                        _botCts.Token
                    );
                    AppendToChat("[SISTEMA]: 🤖 Jack GTR9 (Telegram) è connesso e in ascolto...", Avalonia.Media.Brushes.SpringGreen);
                }
                catch (Exception ex)
                {
                    AppendToChat($"[ERRORE TELEGRAM]: Impossibile avviare il bot. Verifica il token. {ex.Message}", Avalonia.Media.Brushes.Red);
                }
            }
            else
            {
                AppendToChat("[SISTEMA]: ⚠️ Nessun Token Telegram impostato. Bot inattivo. (Configuralo nelle Impostazioni)", Avalonia.Media.Brushes.Orange);
            }

            // --- INIZIALIZZAZIONE UI MEMORIA STORICA ---
            InitializeSidebarEvents();
            RefreshChatHistoryUI(); // Carica subito la lista invisibile all'avvio
        }

	// --- LA SENTINELLA DI AVVIO ---
        protected override async void OnOpened(EventArgs e)
        {
            base.OnOpened(e);

            var configManager = new ConfigManager();
            string llamaPath = configManager.CurrentConfig.LlamaServerPath;

            // Controlla se il percorso è vuoto o il file non esiste
            if (string.IsNullOrWhiteSpace(llamaPath) || !System.IO.File.Exists(llamaPath))
            {
                // 1. Apre il popup di avviso (che blocca l'app in background)
                var warningWindow = new LlamaWarningWindow();
                await warningWindow.ShowDialog(this); 

                // 2. Appena l'utente chiude il popup, gli apriamo in faccia le Impostazioni!
                var settingsWindow = new SettingsWindow(configManager);
                await settingsWindow.ShowDialog(this);
            }
        }

	// --- MOTORE MENU SUPERIORE ---
        private async void OnMenuSettingsClicked(object? sender, RoutedEventArgs e)
        {
            // Istanzia e apre la nuova finestra passandogli il Gestore delle Impostazioni
            var settingsWindow = new SettingsWindow(_configManager);
            
            // ShowDialog blocca l'interazione con la finestra dietro finché non hai finito
            await settingsWindow.ShowDialog(this);
            
            AppendToChat("[SISTEMA]: ⚙️ Impostazioni salvate. Riavviare o usare lo Swap Infrastruttura per caricare i nuovi modelli.", Brushes.Orange);
        }

        // --- MOTORE TEXT-TO-SPEECH (LA VOCE PAOLA) ---
        private async Task SpeakAsync(string textToSpeak)
        {
            string cleanText = Regex.Replace(textToSpeak, @"```.*?```", "", RegexOptions.Singleline);
            cleanText = cleanText.Replace("*", "").Replace("#", "").Replace("`", "").Replace("\"", "");
            
            if (string.IsNullOrWhiteSpace(cleanText)) return;

            string piperDir = "/home/spiderman/ai_models/piper";
            string audioOut = "/tmp/opera_voice_response.wav";
            
            string bashCommand = $"echo \"{cleanText}\" | {piperDir}/piper/piper -m {piperDir}/it_IT-paola-medium.onnx -f {audioOut} && aplay {audioOut}";

            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c '{bashCommand}'",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppendToChat($"[ERRORE VOCE]: Impossibile riprodurre l'audio. {ex.Message}", Brushes.Red);
            }
        }

        // --- GESTIONE MICROFONO (WHISPER) ---
        private async void OnMicButtonClicked(object? sender, RoutedEventArgs e)
        {
            var micButton = sender as Button;
            if (!_isRecording)
            {
                _isRecording = true;
                if (micButton != null) micButton.Foreground = Brushes.Red;
                AppendToChat("[SISTEMA]: 🎙️ In ascolto... (Parla, poi riclicca il microfono)", Brushes.LightSkyBlue);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "arecord",
                    Arguments = $"-f S16_LE -c 1 -r 16000 -q \"{_audioTempPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                _audioProcess = System.Diagnostics.Process.Start(psi);
            }
            else
            {
                _isRecording = false;
                if (micButton != null) micButton.Foreground = Brushes.Gray;
                
                if (_audioProcess != null && !_audioProcess.HasExited)
                {
                    _audioProcess.Kill();
                    _audioProcess.Dispose();
                }

                AppendToChat("[SISTEMA]: ⚙️ Trascrizione in corso...", Brushes.Gray);

                if (File.Exists(_audioTempPath))
                {
                    try
                    {
                        using var form = new MultipartFormDataContent();
                        var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(_audioTempPath));
                        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
                        form.Add(fileContent, "file", "dictation.wav");
                        
                        var response = await _httpClient.PostAsync("http://localhost:8080/inference", form);
                        response.EnsureSuccessStatusCode();
                        
                        var jsonResult = await response.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(jsonResult);
                        string transcript = doc.RootElement.GetProperty("text").GetString()?.Trim() ?? "";

                        if (!string.IsNullOrEmpty(transcript))
                        {
                            var inputTextBox = this.FindControl<TextBox>("UserInputTextBox");
                            if (inputTextBox != null)
                            {
                                inputTextBox.Text += (string.IsNullOrEmpty(inputTextBox.Text) ? "" : " ") + transcript;
                                AppendToChat($"[WHISPER]: \"{transcript}\"", Brushes.Yellow);
                                _isVoiceSession = true; 
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendToChat($"[ERRORE WHISPER]: {ex.Message}", Brushes.Red);
                    }
                }
            }
        }

        // ==========================================================
        // PATCH ARCHITETTURALE: INIETTORE UNIVERSALE (GRAFFETTA)
        // ==========================================================
        private async void OnAttachButtonClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            // Dichiariamo la variabile una sola volta all'inizio per evitare conflitti di scope
            var btnGraffetta = sender as Avalonia.Controls.Button;
            if (btnGraffetta != null) btnGraffetta.IsEnabled = false;

            try
            {
                var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Seleziona Immagine o File di Codice",
                    AllowMultiple = true, // Ora puoi caricare più file in un colpo solo!
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Tutti i File Supportati") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.cs", "*.axaml", "*.xml", "*.json", "*.txt", "*.md" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("Immagini (Vision)") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("Codice Sorgente") { Patterns = new[] { "*.cs", "*.axaml", "*.xml", "*.json", "*.txt", "*.md" } }
                    }
                });

                if (files != null && files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string path = file.Path.LocalPath;
                        string ext = System.IO.Path.GetExtension(path).ToLower();

                        // SMISTAMENTO INTELLIGENTE
                        if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                        {
                            AttachImageFromPath(path);
                        }
                        else
                        {
                            // === LOGICA CODICE SORGENTE E TESTO ===
                            string content = await System.IO.File.ReadAllTextAsync(path);
                            
                            string lang = ext == ".cs" ? "csharp" : (ext == ".axaml" || ext == ".xml" ? "xml" : "text");
                            if (ext == ".json") lang = "json";

                            string injection = $"\n\nEcco il file '{file.Name}':\n```{lang}\n{content}\n```\n";

                            var txtInput = this.FindControl<Avalonia.Controls.TextBox>("UserInputTextBox");
                            if (txtInput != null)
                            {
                                Dispatcher.UIThread.Post(() => InsertTextAtCaret(txtInput, injection));
                            }

                            Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 📎 File sorgente '{file.Name}' iniettato nel form.", Avalonia.Media.Brushes.LightGray));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE GRAFFETTA]: {ex.Message}", Avalonia.Media.Brushes.Red));
            }
            finally
            {
                // Riabilitiamo il bottone in sicurezza
                if (btnGraffetta != null) btnGraffetta.IsEnabled = true;
            }
        }
        
        private void OnRemoveImageButtonClicked(object? sender, RoutedEventArgs e)
        {
            if (_currentImagePath != null)
            {
                _currentImagePath = null;
                
                var previewContainer = this.FindControl<Border>("ImagePreviewContainer");
                if (previewContainer != null) previewContainer.IsVisible = false;
                
                AppendToChat($"[SISTEMA]: 🗑️ Immagine rimossa.", Brushes.Gray);
            }
        }

        private void OnProjectSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selector = sender as ComboBox;
            if (selector != null && selector.SelectedItem != null)
            {
                string selectedPath = selector.SelectedItem.ToString()!;
                if (selectedPath != _currentWorkspacePath) LoadWorkspace(selectedPath);
            }
        }

        private async void OnBrowseButtonClicked(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Seleziona la cartella del progetto", AllowMultiple = false });
            if (folders.Count > 0) LoadWorkspace(folders[0].Path.LocalPath);
        }

        private void LoadWorkspace(string path)
        {
            if (Directory.Exists(path))
            {
                _currentWorkspacePath = path;
                
                // --- INIEZIONE: FORZA LA SCRIVANIA PULITA ---
                StartNewChatSession(); 

                _workspaceManager = new WorkspaceManager(path);
                _reactiveWorkspace = new ReactiveWorkspace();
                _reactiveWorkspace.StartMonitoring(path, _workspaceManager, _vectorMemory);

                // --- INIZIO PATCH MEMORIA DI PROGETTO (FASE 3) ---
                string nexusDir = Path.Combine(path, ".nexus");
                string memoryDir = Path.Combine(nexusDir, "memory");
                Directory.CreateDirectory(nexusDir);
                Directory.CreateDirectory(memoryDir);
                
                string blueprintPath = Path.Combine(nexusDir, "blueprint.md");
                if (!File.Exists(blueprintPath))
                {
                    string template = "# Obiettivo Principale del Progetto\n[Descrivi qui la visione finale del software...]\n\n## Stato Attuale\n* Modulo base: Inizializzato\n\n## Prossimi Step Architetturali\n1. Implementare architettura...\n2. Testare componenti...";
                    File.WriteAllText(blueprintPath, template);
                }

                // Inizializzazione State JSON Files (Il Cervello del Progetto)
                var initialStates = new Dictionary<string, string>
                {
                    { "decisions.json", "[\n  {\n    \"Date\": \"" + DateTime.Now.ToString("yyyy-MM-dd") + "\",\n    \"Decision\": \"Inizializzazione Progetto\",\n    \"Reason\": \"Avvio del workspace\"\n  }\n]" },
                    { "architecture.json", "{\n  \"DesignPattern\": \"MVC\",\n  \"CoreDependencies\": [\"Avalonia 11.1.0\"]\n}" },
                    { "todo.json", "[\n  {\n    \"Task\": \"Definire la logica di base\",\n    \"Status\": \"Pending\"\n  }\n]" },
                    // --- FIX FASE 4: MEMORIA A LUNGO TERMINE DEL CODER ---
                    { "error_ledger.json", "[\n  \"[SISTEMA]: Questo è il registro degli errori passati. Il Coder NON deve mai ripetere questi errori.\"\n]" }
                };

                foreach (var state in initialStates)
                {
                    string statePath = Path.Combine(memoryDir, state.Key);
                    if (!File.Exists(statePath)) File.WriteAllText(statePath, state.Value);
                }

                // Aggiorniamo a caldo la coscienza del Mentor nella chat attiva
                if (_chatHistory.Count > 0 && _chatHistory[0]["role"] == "system")
                {
                    _chatHistory[0]["content"] = GetDynamicSystemPrompt();
                }

                var blueprintButton = this.FindControl<Button>("OpenBlueprintButton");
                if (blueprintButton != null) blueprintButton.IsVisible = true;

                var closeButton = this.FindControl<Button>("CloseProjectButton");
                if (closeButton != null) closeButton.IsVisible = true;
                // --- FINE PATCH MEMORIA ---

                if (_loadedProjectsHistory.Add(path))
                {
                    var selector = this.FindControl<ComboBox>("ProjectSelector");
                    if (selector != null)
                    {
                        selector.ItemsSource = _loadedProjectsHistory.ToList();
                        selector.SelectedItem = path;
                    }
                }

                var statusText = this.FindControl<TextBlock>("ContextStatusText");
                if (statusText != null) statusText.Text = $"Infrastruttura indicizzata da: {path}";
                var progressBar = this.FindControl<ProgressBar>("ContextProgressBar");
                if (progressBar != null) progressBar.Value = 100;

                var fileList = this.FindControl<ListBox>("ActiveContextFilesListBox");
                if (fileList != null) fileList.ItemsSource = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories).Where(f => !f.Contains("/bin/") && !f.Contains("/obj/")).ToList();
                
                var indexBtn = this.FindControl<Button>("IndexButton");
                if (indexBtn != null) indexBtn.IsEnabled = true;

                AppendToChat($"[SISTEMA]: Progetto '{Path.GetFileName(path)}' caricato in RAM. Master Blueprint sincronizzato.", Avalonia.Media.Brushes.LightGreen);
            }
        }

        private async void OnIndexButtonClicked(object? sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentWorkspacePath)) return;
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            AppendToChat("[SISTEMA]: 🧠 Inizio indicizzazione vettoriale...", Brushes.LightSkyBlue);
            var files = Directory.GetFiles(_currentWorkspacePath, "*.*", SearchOption.AllDirectories);
            int frammentiArchiviati = 0;

            foreach(var file in files)
            {
                if (file.Contains("/bin/") || file.Contains("/obj/") || file.Contains("\\bin\\") || file.Contains("\\obj\\") || file.Contains(".git") || file.EndsWith(".dll") || file.EndsWith(".exe") || file.EndsWith(".png") || file.EndsWith(".jpg") || file.EndsWith(".old"))
                    continue;

                try
                {
                    string content = await File.ReadAllTextAsync(file);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        int chunkSize = 1500;
                        for (int i = 0; i < content.Length; i += chunkSize)
                        {
                            string chunk = content.Substring(i, Math.Min(chunkSize, content.Length - i));
                            string fileLabel = $"{file} [Parte {(i / chunkSize) + 1}]";
                            await _vectorMemory.MemorizeContentAsync(fileLabel, chunk);
                            frammentiArchiviati++;
                        }
                    }
                }
                catch { }
            }
            
            AppendToChat($"[SISTEMA]: ✅ {frammentiArchiviati} frammenti del progetto archiviati.", Brushes.LightGreen);
            if (btn != null) btn.IsEnabled = true;
        }

        // --- ROUTING MESSAGGI (L0 ROUTER E ARCHITETTO) ---
        private async void OnSendButtonClicked(object? sender, RoutedEventArgs e)
        {
            var inputTextBox = this.FindControl<TextBox>("UserInputTextBox");
            var sendBtn = this.FindControl<Button>("SendButton");
            
            if (inputTextBox == null || (string.IsNullOrWhiteSpace(inputTextBox.Text) && string.IsNullOrEmpty(_currentImagePath))) return;

            // BLINDATURA: Disabilita UI durante l'elaborazione per evitare doppi invii
            inputTextBox.IsEnabled = false;
            if (sendBtn != null) sendBtn.IsEnabled = false;

            try
            {
                string userText = inputTextBox.Text ?? "";
                inputTextBox.Text = "";

                // --- PATCH BUG TITOLI CHAT: Rinomina "Nuova Conversazione" subito al primo invio ---
                if (_currentSession.Title == "Nuova Conversazione" || _currentSession.Title.StartsWith("Chat del"))
                {
                    // Prende i primi 30 caratteri della tua domanda per dare un nome al tab
                    string newTitle = userText.Length > 30 ? userText.Substring(0, 30) + "..." : userText;
                    _currentSession.Title = newTitle;
                    
                    // Forza l'aggiornamento grafico della lista a sinistra
                    Dispatcher.UIThread.Post(() => RefreshChatHistoryUI());
                }
                // -----------------------------------------------------------------------------------

                bool useVoiceForThisChain = _isVoiceSession;
                _isVoiceSession = false; 

                long currentTelegramChatId = _lastTelegramChatId;
                _lastTelegramChatId = 0; 

                // --- NUOVO: INTERCETTAZIONE COMANDO ACCADEMIA ---
                if (userText.Trim().StartsWith("/addestra", StringComparison.OrdinalIgnoreCase))
                {
                    AppendToChat($"[EMANUELE]: {userText}", Brushes.White);
                    await HandleTrainingModeAsync(userText.Replace("/addestra", "").Trim());
                    return; // Blocchiamo il flusso normale
                }

                // --- SALVAGENTE PER DIMENTICANZA SLASH ---
                string textLower = userText.Trim().ToLower();
                if (!textLower.StartsWith("/addestra") && 
                   (textLower.StartsWith("addestra ") || textLower.StartsWith("studia ") || textLower.Contains("addestra jak")))
                {
                    AppendToChat($"[EMANUELE]: {userText}", Brushes.White);
                    AppendToChat("[MASTER MENTOR]: Emanuele, percepisco che vuoi avviare una sessione di studio dell'Accademia. Per innescare il Crawler e l'addestramento autonomo, ricordati di usare il comando di sistema! Riscrivi la tua direttiva iniziando con: /addestra [argomento]", Brushes.LightGreen);
                    return;
                }

                string visualFeedback = !string.IsNullOrEmpty(_currentImagePath) ? $"👁️ [IMG: {Path.GetFileName(_currentImagePath)}] " : "";
                AppendToChat($"[EMANUELE]: {visualFeedback}{userText}", Brushes.White);

                if (!string.IsNullOrEmpty(_currentImagePath))
                {
                    if (string.IsNullOrWhiteSpace(userText)) userText = "Analizza in dettaglio questa immagine tecnica e preparala per il Mentore.";
                    await HandleJakAssistantAsync(userText, currentTelegramChatId, useVoiceForThisChain);
                }
                else
                {
                    await InvokeArchitectAsync(userText, currentTelegramChatId, useVoiceForThisChain);
                }
            }
            finally
            {
                // RIATTIVAZIONE UI: Il blocco 'finally' garantisce l'esecuzione matematica di queste righe
                inputTextBox.IsEnabled = true;
                if (sendBtn != null) sendBtn.IsEnabled = true;
                inputTextBox.Focus();
            }
        }

        // --- MOTORE ARCHITETTO (HUB & SPOKE CON PIPELINE UNIFICATA DB + WEB) ---
        private async Task InvokeArchitectAsync(string userText, long currentTelegramChatId, bool useVoiceForThisChain)
        {
            // 1. CHIAMATA AL NUOVO ORCHESTRATORE (SWAP A CALDO)
            await SmartModelSwapAsync("Coder_Principale", "MasterMentor_Architetto_Segugio", "⚖️ Preparazione Aula di Tribunale. Richiamo dell'Architetto in corso...");

            // 2. RECUPERO DEL SELETTORE
            var modeSelector = this.FindControl<ComboBox>("ModeSelector"); 
            bool isProgrammingMode = modeSelector == null || modeSelector.SelectedIndex == 0; 

            // --- INIZIO PATCH: BYPASS RAG DURANTE ESCALATION ---
            bool isSystemEscalation = userText.StartsWith("[SISTEMA: ESCALATION CRITICA");
            string contextData = ""; // Dichiariamo fuori per poterlo usare dopo

            if (!isSystemEscalation)
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 🧠 Ricerca simultanea su Database Vettoriale e Rete Web in corso...", Avalonia.Media.Brushes.Cyan));

                // 1. Eseguiamo la ricerca locale (Qdrant) e quella esterna (Web) IN PARALLELO
                Task<string> webSearchTask = Task.Run(() => ExecuteBackgroundWebResearchAsync(userText));
                Task<List<string>> qdrantTask = _vectorMemory.SearchContextAsync(userText, topK: 4);

                await Task.WhenAll(webSearchTask, qdrantTask); 

                // --- RADAR DI RETE E TOLLERANZA GUASTI ---
                string webContext = "";
                try 
                {
                    webContext = await webSearchTask; // Estraiamo il risultato del web

                    // Rilevamento dei blocchi tipici dei motori di ricerca (Anti-Bot) o errori di rete
                    if (webContext.Contains("403 Forbidden") || webContext.Contains("429 Too Many Requests") || webContext.Contains("Impossibile connettersi"))
                    {
                        Dispatcher.UIThread.Post(() => AppendToChat("[⚠️ ALLARME RETE]: Il motore di ricerca ha bloccato la richiesta (Anti-Bot) o la connessione è instabile. Il sistema passa in modalità 'Sopravvivenza' usando SOLO il Database Locale.", Avalonia.Media.Brushes.OrangeRed));
                        webContext = ""; // Svuotiamo per non inquinare la mente dell'Architetto
                    }
                }
                catch (Exception)
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[⚠️ ALLARME RETE]: Rete assente o server irraggiungibile. L'IDE opererà esclusivamente tramite RAG Locale.", Avalonia.Media.Brushes.OrangeRed));
                    webContext = ""; 
                }

                List<string> libraryResults = qdrantTask.Result;

                if (libraryResults.Count > 0)
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[ACCADEMIA]: 📚 Estratti {libraryResults.Count} frammenti di letteratura dal database locale.", Avalonia.Media.Brushes.LightGreen));
                    contextData += "=== DATI AZIENDALI (QDRANT) ===\n" + string.Join("\n\n", libraryResults) + "\n===============================\n\n";
                }
                
                if (!string.IsNullOrEmpty(webContext) && !webContext.StartsWith("[INFO]"))
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[SEGUGIO WEB]: 🌐 Trovati dati freschi da internet. Iniezione nel contesto neurale...", Avalonia.Media.Brushes.LightGreen));
                    contextData += "=== DATI FRESCHI DA INTERNET ===\n" + webContext + "\n================================\n\n";

                    // Archiviazione automatica intelligente (ANTI-DUPLICATO)
                    _ = Task.Run(async () => 
                    {
                        try {
                            // 1. Verifichiamo se il DB ci ha già restituito questo stesso identico contesto oggi o in passato
                            bool isAlreadyKnown = libraryResults.Any(dbDoc => 
                                dbDoc == webContext || 
                                (dbDoc.Length > 100 && webContext.Contains(dbDoc.Substring(0, 100)))
                            );

                            if (!isAlreadyKnown)
                            {
                                string safeTitle = userText.Length > 20 ? userText.Substring(0, 20) : userText;
                                string safeLabel = $"[SKILL_WEB_{DateTime.Now:yyyyMMdd}] {safeTitle}";
                                await _vectorMemory.MemorizeContentAsync(safeLabel, webContext);
                                Dispatcher.UIThread.Post(() => AppendToChat($"[⚙️ COSCIENZA]: Nuovi dati web cristallizzati permanentemente nel database.", Avalonia.Media.Brushes.SpringGreen));
                            }
                            else
                            {
                                Dispatcher.UIThread.Post(() => AppendToChat($"[⚙️ COSCIENZA]: Dati web scartati (conoscenza già assimilata in precedenza).", Avalonia.Media.Brushes.Gray));
                            }
                        } catch { }
                    });
                }
            }
            else
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 🚨 Escalation in corso. Moduli RAG disabilitati per garantire la purezza diagnostica.", Avalonia.Media.Brushes.Orange));
            }
            // --- FINE PATCH BYPASS ---

            // FIX CRITICO JINJA: Assicuriamoci che l'ultimo ruolo NON sia già 'user' prima di aggiungere.
            // Se c'è stato un errore al giro precedente, rimuoviamo l'orfanello per mantenere l'alternanza.
            if (_chatHistory.Count > 0 && _chatHistory.Last()["role"] == "user")
            {
                _chatHistory.RemoveAt(_chatHistory.Count - 1);
            }

            // FIX CONTEXT BLOAT: Salviamo SOLO il testo puro dell'utente (o del Sistema) nella cronologia permanente
            _chatHistory.Add(new Dictionary<string, string> { { "role", "user" }, { "content", userText } });
            _currentSession.Messages.Add(new ChatMessage { Role = "user", Content = userText });
            _sessionManager.SaveSession(_currentSession, _currentWorkspacePath);

            // Prepariamo un "clone" temporaneo della chat da inviare al server solo per questo giro
            var tempHistory = new List<Dictionary<string, string>>(_chatHistory);
            if (!string.IsNullOrEmpty(contextData))
            {
                // Inietta il papiro di documenti SOLO per l'inferenza attuale, di nascosto
                tempHistory[tempHistory.Count - 1] = new Dictionary<string, string> 
                { 
                    { "role", "user" }, 
                    { "content", $"[CONTESTO EXTRA RAG/WEB]:\n{contextData}\n\nDOMANDA DELL'UTENTE: {userText}" } 
                };
            }

            // 3. Il Master Mentor riceve la chat clonata e risponde
            string masterAnalysis = await StreamMasterLocalAsync(tempHistory);

            if (useVoiceForThisChain) await SpeakAsync(masterAnalysis);

            // 4. Smistamento Intelligente (Agentic Workflow Ibrido)
            if (isProgrammingMode)
            {
                if (masterAnalysis.Contains("[GENERA_CODICE]"))
                {
                    string cleanAnalysis = masterAnalysis.Replace("[GENERA_CODICE]", "").Trim();
                    
                    // FIX: Non inviamo più il fusionPrompt gigante. Inviamo solo l'analisi pulita!
                    // Il metodo DelegateToCoderAsync si occuperà da solo di iniettare i file e la chat.
                    await DelegateToCoderAsync(cleanAnalysis, useVoiceForThisChain);
                }
                else
                {
                    Dispatcher.UIThread.Post(() => 
                    {
                        var (_, container) = AppendToChat("[SISTEMA]: 🧠 Architetto in attesa. Vuoi che il Coder scriva l'implementazione?", Avalonia.Media.Brushes.Gray);
                        
                        var delegateBtn = new Button { 
                            Content = "💻 Delega al Coder (8082)", 
                            Background = Avalonia.Media.Brushes.DodgerBlue, 
                            Foreground = Avalonia.Media.Brushes.White, 
                            Margin = new Avalonia.Thickness(0, 10, 0, 0),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                        };
                        
                        delegateBtn.Click += async (s, ev) => 
                        {
                            delegateBtn.IsEnabled = false;
                            delegateBtn.Content = "⏳ Elaborazione Coder in corso...";
                            
                            // FIX: Anche nel bottone manuale, passiamo direttamente l'analisi del Mentor
                            await DelegateToCoderAsync(masterAnalysis, useVoiceForThisChain);
                        };
                        
                        container.Children.Add(delegateBtn);
                    });
                }
            }
            else
            {
                // In modalità Accademia stampiamo solo la conferma.
                // Il bottone manuale è stato ELIMINATO perché i dati web vengono
                // già cristallizzati in automatico in cima al metodo!
                Dispatcher.UIThread.Post(() => AppendToChat("[SISTEMA]: ✅ Analisi Scientifica/Legale completata.", Avalonia.Media.Brushes.Gold));
            }

            if (_currentSession.Title.StartsWith("Chat del") && _currentSession.Messages.Count == 2)
            {
                _ = Task.Run(() => AutoRenameSessionAsync());
            }
        }

        // --- STREAMING ISOLATO DEL MASTER MENTOR ---
        private async Task<string> StreamMasterLocalAsync(List<Dictionary<string, string>> payloadHistory)
        {
            // FIX ANTI-LOOP: Temperatura abbassata per logica rigida, Presence Penalty alzata per forzare il cambio di discorso
            // FIX ANTI-AMMUTOLIMENTO: Azzeriamo le penalità che strangolavano lo stream, 
            // ma teniamo la temperatura a 0.1 per forzare il modello a essere un calcolatore freddo e spietato.
            var payload = new { 
                messages = payloadHistory, 
                temperature = 0.1, 
                max_tokens = 2048, 
                stream = true, 
                frequency_penalty = 0.0, 
                presence_penalty = 0.0 
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8081/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            StringBuilder fullResponse = new StringBuilder();
            try
            {
                var (aiMessageBlock, _) = AppendToChat("[MASTER MENTOR]:\n", Brushes.LightGreen, true, "[MASTER MENTOR]:\n");
                var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
                var scrollViewer = chatPanel?.Parent as ScrollViewer;

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE SERVER 8081]: {errorContent}", Brushes.Red));
                    
                    // FIX BILANCIAMENTO JINJA: Inseriamo una finta risposta dell'assistente per chiudere il loop
                    _chatHistory.Add(new Dictionary<string, string> { { "role", "assistant" }, { "content", "[Errore di generazione. L'Architetto ha perso la connessione temporaneamente.]" } });
                    
                    return "";
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: ") && line.Substring(6) != "[DONE]")
                    {
                        var doc = JsonDocument.Parse(line.Substring(6));
                        var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement))
                        {
                            string chunk = contentElement.GetString() ?? "";
                            fullResponse.Append(chunk);
                            Dispatcher.UIThread.Post(() => { aiMessageBlock.Text += chunk; scrollViewer?.ScrollToEnd(); });
                        }
                    }
                }
                
                // Salvataggio pulito della risposta del Mentor
                _chatHistory.Add(new Dictionary<string, string> { { "role", "assistant" }, { "content", fullResponse.ToString() } });
                _currentSession.Messages.Add(new ChatMessage { Role = "assistant", Content = fullResponse.ToString() });
                _sessionManager.SaveSession(_currentSession, _currentWorkspacePath);
            }
            catch (Exception ex) { Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE MASTER]: {ex.Message}", Brushes.Red)); }

            return fullResponse.ToString();
        }

	// --- MOTORE SEGUGIO: RICERCA ED ESTRAZIONE IN BACKGROUND ---
        private async Task<string> ExecuteBackgroundWebResearchAsync(string query)
        {
            try
            {
                // Utilizziamo un'interrogazione mirata via DuckDuckGo HTML o SearxNG locale priva di chiavi API
                string searchUrl = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                
                var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return "[INFO]: Impossibile raggiungere il motore di ricerca esterno.";

                string htmlContent = await response.Content.ReadAsStringAsync();
                
                // Estraiamo i primi 3 link puliti usando le espressioni regolari sul sorgente HTML
                var linkMatches = Regex.Matches(htmlContent, @"<a class=""result__url"" href=""(?<url>[^""]+)""");
                List<string> urlsToScrape = new List<string>();

                foreach (Match match in linkMatches)
                {
                    string url = match.Groups["url"].Value;
                    // Pulizia dei redirect interni di DuckDuckGo
                    if (url.Contains("uddg="))
                    {
                        url = Uri.UnescapeDataString(url.Split("uddg=")[1].Split("&")[0]);
                    }
                    if (!url.Contains("wikipedia.org") && urlsToScrape.Count < 3) // Evitiamo loop su wiki se vogliamo dati freschi
                    {
                        urlsToScrape.Add(url);
                    }
                }

                StringBuilder compiledWebContext = new StringBuilder();
                
                // Scraping parallelo dei contenuti delle pagine web trovate
                foreach (var url in urlsToScrape)
                {
                    try
                    {
                        var pageRequest = new HttpRequestMessage(HttpMethod.Get, url);
                        pageRequest.Headers.Add("User-Agent", "Mozilla/5.0");
                        var pageResponse = await _httpClient.SendAsync(pageRequest);
                        
                        if (pageResponse.IsSuccessStatusCode)
                        {
                            string rawHtml = await pageResponse.Content.ReadAsStringAsync();
                            // Rimuoviamo script, tag html e css per estrarre solo il testo leggibile
                            string cleanText = Regex.Replace(rawHtml, @"<script[^>]*>[\s\S]*?</script>", "");
                            cleanText = Regex.Replace(cleanText, @"<style[^>]*>[\s\S]*?</style>", "");
                            cleanText = Regex.Replace(cleanText, @"<[^>]+>", " ");
                            cleanText = Regex.Replace(cleanText, @"\s+", " ").Trim();

                            // Prendiamo i primi 1500 caratteri più significativi della pagina
                            string abstractText = cleanText.Length > 1500 ? cleanText.Substring(0, 1500) : cleanText;
                            compiledWebContext.AppendLine($"[FONTE AGGIORNATA: {url}]\n{abstractText}\n---");
                        }
                    }
                    catch { /* Ignoriamo i singoli fallimenti di connessione ai siti */ }
                }

                return compiledWebContext.Length > 0 ? compiledWebContext.ToString() : "[INFO]: Nessun dato utile estratto dal Web.";
            }
            catch (Exception ex)
            {
                return $"[INFO]: Fallimento totale della ricerca in background. Det: {ex.Message}";
            }
        }

        // --- MOTORE CODER (MULT-AGENT STATE CON CHATML NATIVO E FILE AWARENESS) ---
        private async Task DelegateToCoderAsync(string plannerAnalysis, bool useVoice)
        {
            AppendToChat("[SISTEMA]: ⚙️ Compilazione del Dossier di Progetto per il Coder (8082)...", Avalonia.Media.Brushes.LightSkyBlue);

            // 1. CHIAMATA AL NUOVO ORCHESTRATORE (SWAP A CALDO)
            await SmartModelSwapAsync("MasterMentor_Architetto_Segugio", "Coder_Principale", "🧠 Allocazione KV Cache estesa. Risveglio Coder (30B) in corso...");

            // 2. CREAZIONE DEL PAYLOAD NATIVO
            var messagesPayload = new List<object>();

            // --- VERSIONE CORRETTA: ISOLAMENTO DEL CONTESTO E MEMORIA A LUNGO TERMINE --- [PATCH APPLICATA]
            string coderLTM = "";
            if (!string.IsNullOrEmpty(_currentWorkspacePath))
            {
                string ledgerPath = Path.Combine(_currentWorkspacePath, ".nexus", "memory", "error_ledger.json");
                if (File.Exists(ledgerPath))
                {
                    try
                    {
                        string jsonContent = await File.ReadAllTextAsync(ledgerPath);
                        List<string>? errors = JsonSerializer.Deserialize<List<string>>(jsonContent);
                        if (errors != null && errors.Count > 1)
                        {
                            // Esclude la nota iniziale del sistema per dare solo i veri log di errore
                            coderLTM = "\n\n=== MEMORIA RECENTE DEGLI ERRORI COMPILATORE (DA NON RIPETERE MAI PIÙ) ===\n" +
                                       string.Join("\n", errors.Skip(1).TakeLast(5)) + // Ottimizza la finestra di contesto agli ultimi 5 errori
                                       "\n=======================================================================\n";
                        }
                    }
                    catch
                    {
                        // Fallback in caso di file ancora corrotto dal vecchio sistema
                        coderLTM = $"\n\n=== REGISTRO ERRORI ===\n{await File.ReadAllTextAsync(ledgerPath)}\n=======================\n";
                    }
                }
            }

            // --- INTERROGAZIONE DELLA COSCIENZA DEI FALLIMENTI (Qdrant RAG) --- [PATCH APPLICATA]
            string failureConscienceContext = "";
            try
            {
                // Cerca nel database vettoriale se ci sono fallimenti logici simili registrati
                var pastFailures = await _vectorMemory.SearchContextAsync($"[COSCIENZA_FALLIMENTO] {plannerAnalysis}", topK: 2);
                if (pastFailures != null && pastFailures.Count > 0)
                {
                    failureConscienceContext = "\n=== MEMORIA DEI FALLIMENTI LOGICI ARCHIVIATI (EVITARE QUESTI PATTERN) ===\n" +
                                               string.Join("\n\n", pastFailures) +
                                               "\n========================================================================\n";
                }
            }
            catch { }

            // 1. REGOLE FERREE DEL CODER (System Message)
            string systemInstruction = $@"Sei il Coder (Ingegnere Riparatore e Sviluppatore) di Opera Suprema. Scrivi SOLO il codice implementativo.
REGOLA 1: Devi SEMPRE dichiarare il nome del file prima del blocco di codice.
REGOLA 2: Genera SEMPRE il file .csproj imponendo TASSATIVAMENTE <TargetFramework>net10.0</TargetFramework>. Includi ESCLUSIVAMENTE questi 5 pacchetti Avalonia 11.1.0: 'Avalonia', 'Avalonia.Desktop', 'Avalonia.Themes.Fluent', 'Avalonia.Diagnostics', 'Avalonia.ReactiveUI'. NON aggiungere altri pacchetti.
REGOLA 3: Per i progetti Avalonia, DEVI SEMPRE generare per intero i file di avvio obbligatori ('Program.cs', 'App.axaml' e 'App.axaml.cs').
REGOLA 4: Usa ESCLUSIVAMENTE Avalonia 11 (XAML moderno, RowDefinitions, niente XamlLoader.Plugins). Usa RequestedThemeVariant=""Dark"" nel tag Application e NON Mode=""Dark"". Assicurati che i costruttori chiamino sempre InitializeComponent();.
REGOLA SUPREMA DI FORMATTAZIONE: Per OGNI SINGOLO FILE, DEVI usare questo esatto formato, pena il kernel panic:
[FILE: Cartella/NomeDelFile.estensione]
```csharp
// codice
```{coderLTM}";
            messagesPayload.Add(new { role = "system", content = systemInstruction });

            // 2. RECUPERO DEL BLUEPRINT (System Context)
            if (!string.IsNullOrEmpty(_currentWorkspacePath))
            {
                string blueprintPath = Path.Combine(_currentWorkspacePath, ".nexus", "blueprint.md");
                if (File.Exists(blueprintPath))
                {
                    string bpContent = await File.ReadAllTextAsync(blueprintPath);
                    messagesPayload.Add(new { role = "system", content = $"=== BLUEPRINT DEL PROGETTO ===\n{bpContent}\n=============================" });
                }
            }

            // 3. PRE-LETTURA DEI FILE SORGENTE
            string injectedFilesContext = "";
            if (!string.IsNullOrEmpty(_currentWorkspacePath))
            {
                HashSet<string> filesContext = new HashSet<string>();
                
                var fileMatches = Regex.Matches(plannerAnalysis, @"\b[\w\-\.]+\.(cs|axaml|csproj|xml|json|md)\b");
                foreach (Match match in fileMatches)
                {
                    string fileName = match.Value;
                    string? realPath = FuzzyFindExistingFile(fileName, _currentWorkspacePath);
                    if (realPath != null && !filesContext.Contains(realPath))
                    {
                        filesContext.Add(realPath);
                    }
                }

                if (filesContext.Count > 0)
                {
                    foreach (var file in filesContext) 
                    {
                        try 
                        {
                            string content = await File.ReadAllTextAsync(file);
                            string relative = file.Replace(_currentWorkspacePath + Path.DirectorySeparatorChar.ToString(), "");
                            injectedFilesContext += $"=== STATO ATTUALE DEL FILE DA MODIFICARE: [{relative}] ===\n```\n{content}\n```\n\n";
                        }
                        catch { }
                    }
                    messagesPayload.Add(new { role = "system", content = $"=== INFORMAZIONI DI CONTESTO (STATO DEL FILE SYSTEM) ===\nIL CODER HA ACCESSO IN LETTURA AI SEGUENTI FILE ESISTENTI NEL PROGETTO:\n{injectedFilesContext}" });
                }
            }

            // 4. ORDINE ESECUTIVO FINALE (User Message)
            string finalCommand = $"L'Architetto ti ha ordinato di procedere con l'implementazione basandoti sul Blueprint, sui file sorgente attuali e sul registro degli errori passati.\nEcco la sua direttiva esecutiva finale:\n\n{plannerAnalysis}\n\nAgisci come Coder. Applica ESATTAMENTE la soluzione. Riscrivi per intero i file necessari. Scrivi SOLO il codice implementativo, senza spiegazioni testuali.";
            messagesPayload.Add(new { role = "user", content = finalCommand });

            var payload = new {
                messages = messagesPayload, 
                temperature = 0.2, 
                max_tokens = 8192, 
                stream = true,
                frequency_penalty = 0.2 
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8082/v1/chat/completions") {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            try
            {
                var (aiMessageBlock, mainContainer) = AppendToChat("[CODER]:\n", Avalonia.Media.Brushes.Cyan, true, "[CODER]:\n");
                var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
                var scrollViewer = chatPanel?.Parent as ScrollViewer;

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE FATALE CODER 8082]: HTTP {(int)response.StatusCode}\n{errorContent}", Avalonia.Media.Brushes.Red));
                    return;
                }

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                StringBuilder coderFullResponse = new StringBuilder();

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: ") && line.Substring(6) != "[DONE]")
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(line.Substring(6));
                        var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement))
                        {
                            string chunk = contentElement.GetString() ?? "";
                            coderFullResponse.Append(chunk);
                            Dispatcher.UIThread.Post(() => 
                            {
                                aiMessageBlock.Text += chunk;
                                scrollViewer?.ScrollToEnd();
                            });
                        }
                    }
                }

                string generatedCode = coderFullResponse.ToString();
                
                // --- FIX JINJA (ALTERNANZA RUOLI): Appendiamo il codice all'ultimo messaggio dell'Architetto ---
                if (_chatHistory.Count > 0 && _chatHistory.Last()["role"] == "assistant")
                {
                    _chatHistory.Last()["content"] += $"\n\n[RISCONTRO DI SISTEMA: IL CODER HA APPENA GENERATO E INCISO QUESTO CODICE]:\n{generatedCode}";
                }
                
                if (_currentSession.Messages.Count > 0 && _currentSession.Messages.Last().Role == "assistant")
                {
                    _currentSession.Messages.Last().Content += $"\n\n[RISCONTRO DI SISTEMA: IL CODER HA APPENA GENERATO E INCISO QUESTO CODICE]:\n{generatedCode}";
                }
                _sessionManager.SaveSession(_currentSession, _currentWorkspacePath);
                // ---------------------------------------------------------------------------

                AutonomousProjectGenerator(generatedCode);
                
                var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

                var saveBtn = new Button { Content = "💾 Incidi su Disco", Background = Avalonia.Media.Brushes.SteelBlue, Foreground = Avalonia.Media.Brushes.White, Margin = new Avalonia.Thickness(0, 10, 10, 0) };
                saveBtn.Click += async (s, ev) => await SaveCodeToDiskAsync(generatedCode);
                buttonPanel.Children.Add(saveBtn);

                var autoBuildBtn = new Button { Content = "⚙️ Auto-Compila e Risolvi", Background = Avalonia.Media.Brushes.Crimson, Foreground = Avalonia.Media.Brushes.White, Margin = new Avalonia.Thickness(0, 10, 10, 0) };
                autoBuildBtn.Click += async (s, ev) => { 
                    autoBuildBtn.IsEnabled = false; 
                    autoBuildBtn.Content = "⏳ Compilazione..."; 
                    await RunAutoCompilationLoopAsync(); 
                };
                buttonPanel.Children.Add(autoBuildBtn);

                var criticCheck = this.FindControl<CheckBox>("AutoCriticCheck");
                if (criticCheck != null && criticCheck.IsChecked == true)
                {
                    mainContainer.Children.Add(buttonPanel);
                    await RunCriticAsync(generatedCode, useVoice); 
                }
                else
                {
                    var criticBtn = new Button { Content = "🧐 Analisi Critica (8081)", Background = Avalonia.Media.Brushes.MediumPurple, Foreground = Avalonia.Media.Brushes.White, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
                    criticBtn.Click += async (s, ev) => { criticBtn.IsEnabled = false; await RunCriticAsync(generatedCode, useVoice); };
                    buttonPanel.Children.Add(criticBtn);
                    mainContainer.Children.Add(buttonPanel);
                }
            }
            catch (Exception ex)
            {
                AppendToChat($"[ERRORE CODER]: {ex.Message}", Avalonia.Media.Brushes.Red);
            }
        }

        // --- MOTORE CRITIC (GIUDICE SUPREMO: GEMMA 3 27B SULLA PORTA 8081) ---
        private async Task RunCriticAsync(string sourceCode, bool useVoice)
        {
            AppendToChat("[SISTEMA]: 🧐 Avvio revisione codice tramite Giudice Supremo (Porta 8081)...", Brushes.LightSkyBlue);

            // --- INIZIO PATCH: FILTRO IMMUNITÀ BOILERPLATE (AMPUTAZIONE FISICA AVANZATA) ---
            string safeCodeToReview = sourceCode;
            string[] filesToProtect = { ".csproj", "Program.cs", "App.axaml", "App.axaml.cs" };

            foreach (var file in filesToProtect)
            {
                // Usiamo una Regex per intercettare il tag a prescindere dal nome della cartella inventata dal Coder
                string pattern = $@"\[FILE:[^\]]*?{System.Text.RegularExpressions.Regex.Escape(file)}\]";
                var match = System.Text.RegularExpressions.Regex.Match(safeCodeToReview, pattern);

                while (match.Success)
                {
                    int startIndex = match.Index;
                    int nextFileIndex = safeCodeToReview.IndexOf("[FILE:", startIndex + match.Length);

                    if (nextFileIndex == -1)
                        safeCodeToReview = safeCodeToReview.Substring(0, startIndex);
                    else
                        safeCodeToReview = safeCodeToReview.Substring(0, startIndex) + safeCodeToReview.Substring(nextFileIndex);
                    
                    match = System.Text.RegularExpressions.Regex.Match(safeCodeToReview, pattern);
                }
            }
            // --- FINE PATCH ---

            string backticks = "\u0060\u0060\u0060";

            // Prompt Spietato per Gemma 3 27B con VIA DI FUGA
            string promptSupreme = $@"Sei il Revisore Supremo del Codice (Senior QA).
Analizza il codice C# fornito per scovare bug logici, I/O bloccante o errori di sintassi.
VIA DI FUGA (ASSOLUZIONE): Se il codice è strutturalmente perfetto, compila correttamente e non richiede modifiche, NON stampare alcun file. Rispondi ESCLUSIVAMENTE con la stringa: [CODICE_APPROVATO].
MODIFICHE (CONDANNA): Se trovi errori, correggili e restituisci SEMPRE i file interi. Non aggiungere spiegazioni testuali.
Prima di OGNI blocco di codice corretto, DEVI usare TASSATIVAMENTE il formato:
[FILE: percorso]
{backticks}csharp
// codice
{backticks}";

            // Inviamo la richiesta a Gemma 3 (Porta 8081)
            var outputSupreme = await StreamCriticResponseAsync("http://localhost:8081/v1/chat/completions", "GIUDICE SUPREMO (Gemma 3)", Brushes.Plum, promptSupreme, safeCodeToReview);

            // Analisi dei fallimenti per la memoria vettoriale
            if (outputSupreme.Contains("ERRORE") || outputSupreme.Contains("Exception"))
                _ = Task.Run(() => ArchiveFailureConscienceAsync(sourceCode, "Critica rilevata dal Giudice Supremo."));
            else
                _ = Task.Run(() => ArchiveSuccessKnowledgeAsync(sourceCode));

            // --- NUOVA LOGICA INFALLIBILE DEL TRIBUNALE ---
            if (outputSupreme.Contains("[CODICE_APPROVATO]"))
            {
                Dispatcher.UIThread.Post(() => AppendToChat("[SISTEMA]: ⚖️ Il Tribunale ha emesso la sentenza: Il codice è perfetto. Nessuna patch necessaria.", Brushes.SpringGreen));
            }
            else
            {
                // UI dei bottoni di salvataggio solo se c'è codice da salvare
                var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };

                if (outputSupreme.Contains(backticks) || outputSupreme.Contains("```"))
                {
                    var saveBtn = new Button { Content = "💾 Incidi Codice Supremo", Background = Brushes.MediumPurple, Foreground = Brushes.White, Margin = new Avalonia.Thickness(0, 10, 10, 0) };
                    saveBtn.Click += (s, ev) => 
                    { 
                        saveBtn.IsEnabled = false;
                        saveBtn.Content = "✅ Patch Suprema Incisa";
                        AutonomousProjectGenerator(outputSupreme); 
                    };
                    buttonPanel.Children.Add(saveBtn);
                }

                var instructionsBtn = new Button { Content = "👨‍🏫 Chiedi Istruzioni Operative", Background = Brushes.Gold, Foreground = Brushes.Black, Margin = new Avalonia.Thickness(0, 10, 0, 0) };
                instructionsBtn.Click += async (s, ev) => {
                    instructionsBtn.IsEnabled = false;
                    await ProvideExecutionInstructionsAsync(outputSupreme, useVoice); 
                };
                buttonPanel.Children.Add(instructionsBtn);

                var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
                Dispatcher.UIThread.Post(() => chatPanel?.Children.Add(buttonPanel));
            }
        }

        // --- HELPER METODO PER LO STREAMING PARALLELO DEL TRIBUNALE ---
        private async Task<string> StreamCriticResponseAsync(string url, string roleName, IBrush color, string systemPrompt, string sourceCode)
        {
            // --- INIEZIONE FORZATA: Aggiungiamo la Regola Suprema al prompt in arrivo ---
            systemPrompt += "\nREGOLA SUPREMA DI FORMATTAZIONE: Per OGNI file corretto, DEVI TASSATIVAMENTE usare questo esatto formato, inclusi i tre apici. Se scrivi codice senza i tre apici (```) il salvataggio fallirà miseramente:\n" +
                            "[FILE: NomeDelFile.estensione]\n" +
                            "```csharp\n" +
                            "// codice corretto\n" +
                            "```\n";

            var payload = new {
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = sourceCode }
                },
                temperature = 0.1, max_tokens = 8192, stream = true, frequency_penalty = 0.0
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url) {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            StringBuilder criticFullResponse = new StringBuilder();
            try
            {
                var (aiMessageBlock, _) = AppendToChat($"[{roleName}]:\n", color, true, $"[{roleName}]:\n");
                var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
                var scrollViewer = chatPanel?.Parent as ScrollViewer;

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return $"[ERRORE {roleName}]: Server HTTP {response.StatusCode} non raggiungibile.";

                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: ") && line.Substring(6) != "[DONE]")
                    {
                        var doc = JsonDocument.Parse(line.Substring(6));
                        var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement))
                        {
                            string chunk = contentElement.GetString() ?? "";
                            criticFullResponse.Append(chunk);
                            Dispatcher.UIThread.Post(() => { aiMessageBlock.Text += chunk; scrollViewer?.ScrollToEnd(); });
                        }
                    }
                }
                return criticFullResponse.ToString();
            }
            catch (Exception ex) { return $"[ERRORE {roleName}]: {ex.Message}"; }
        }

        // --- MOTORE ISTRUZIONI E CHIUSURA LOOP ---
        private async Task ProvideExecutionInstructionsAsync(string code, bool useVoice)
        {
            AppendToChat("[SISTEMA]: 👨‍🏫 L'Architetto sta preparando le istruzioni operative...", Brushes.LightSkyBlue);

            string instructionPrompt = $@"Analizza rapidamente le modifiche apportate in questo codice.
Fornisci ESCLUSIVAMENTE i comandi bash da terminale (come 'dotnet run' o 'dotnet build') necessari all'utente per testare l'app.
Metti i comandi in un blocco codice ```bash. Non aggiungere altre spiegazioni.";

            // Non aggiungiamo questo prompt tecnico nascosto alla cronologia utente per non sporcarla!
            var payload = new
            {
                messages = new[] {
                    new { role = "system", content = "Sei il Master Mentor." },
                    new { role = "user", content = instructionPrompt }
                },
                temperature = 0.1,
                max_tokens = 500,
                stream = true
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8081/v1/chat/completions")
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            try
            {
                var (aiMessageBlock, mainContainer) = AppendToChat("[MENTORE]:\n", Brushes.Gold, true, "[MENTORE]:\n");
                var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
                var scrollViewer = chatPanel?.Parent as ScrollViewer;

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);
                System.Text.StringBuilder fullResponse = new System.Text.StringBuilder();

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: ") && line.Substring(6) != "[DONE]")
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(line.Substring(6));
                        var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement))
                        {
                            string chunk = contentElement.GetString() ?? "";
                            fullResponse.Append(chunk);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                            { 
                                aiMessageBlock.Text += chunk; 
                                scrollViewer?.ScrollToEnd(); 
                            });
                        }
                    }
                }

                // Chiudiamo il Loop: Registriamo fittiziamente un "passaggio di stato" nella cronologia per mantenere l'alternanza Jinja felice
                _chatHistory.Add(new System.Collections.Generic.Dictionary<string, string> { { "role", "user" }, { "content", "Fase di produzione conclusa. Quali sono i comandi di avvio?" } });
                _chatHistory.Add(new System.Collections.Generic.Dictionary<string, string> { { "role", "assistant" }, { "content", fullResponse.ToString() } });

                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    AppendToChat("\n[SISTEMA]: 🟢 Ciclo di Produzione completato. Il Master Mentor è in ascolto per errori di compilazione o nuove direttive.", Brushes.SpringGreen);
                });
            }
            catch (System.Exception ex)
            {
                AppendToChat($"[ERRORE MENTORE]: {ex.Message}", Brushes.Red);
            }
        }

        // --- SALVATAGGIO FISICO SU DISCO ---
        private async Task SaveCodeToDiskAsync(string rawOutput)
        {
            string codeToSave = rawOutput;
            var match = Regex.Match(rawOutput, @"```(?:csharp|cs)?\s*(.*?)\s*```", RegexOptions.Singleline);
            if (match.Success) codeToSave = match.Groups[1].Value;

            IStorageFolder? startFolder = null;
            if (!string.IsNullOrEmpty(_currentWorkspacePath))
            {
                startFolder = await StorageProvider.TryGetFolderFromPathAsync(new Uri($"file://{_currentWorkspacePath}"));
            }

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Incidi codice su disco",
                SuggestedFileName = "NuovaClasse.cs",
                DefaultExtension = ".cs",
                SuggestedStartLocation = startFolder
            });

            if (file != null)
            {
                await File.WriteAllTextAsync(file.Path.LocalPath, codeToSave);
                AppendToChat($"[SISTEMA]: 💾 File salvato con successo in {file.Path.LocalPath}", Brushes.LightGreen);
            }
        }

	// =========================================================================
        // MOTORIE GENESI AUTONOMA CON AUTO-EPURAZIONE INTELLIGENTE (GHOST FILES FIX)
        // =========================================================================
        private void AutonomousProjectGenerator(string aiOutput)
        {
            if (string.IsNullOrWhiteSpace(aiOutput)) return;

            // Pattern adattivo: cattura sia [FILE: path] che // FILE: path, seguito dal blocco ```csharp
            string pattern = @"(?:\[FILE:\s*|//\s*FILE:\s*)(?<path>[^\]\r\n]+)(?:\]|\r?\n)\s*```[a-zA-Z#]*\s*(?<code>.*?)\s*```";
            var matches = Regex.Matches(aiOutput, pattern, RegexOptions.Singleline);

            if (matches.Count == 0)
            {
                Dispatcher.UIThread.Post(() => AppendToChat("[SISTEMA]: ⚠️ Nessun tag di automazione [FILE: ...] rilevato per la scrittura automatica.", Brushes.Orange));
                return;
            }

            // --- FASE 1: PRE-PROCESSING E RICERCA DELLA ROOT DEL PROGETTO ---
            List<(string RawPath, string FinalPath, string Code)> parsedFiles = new();
            string? projectRootToPurge = null;
            bool isFullProjectGeneration = false; // <-- NUOVO CHECK INTELLIGENTE

            foreach (Match match in matches)
            {
                string rawPath = match.Groups["path"].Value.Trim();
                string codeContent = match.Groups["code"].Value;
                string finalPath = rawPath;

                if (rawPath.StartsWith("~"))
                {
                    finalPath = rawPath.Replace("~", "/home/spiderman");
                }
                else if (!Path.IsPathRooted(rawPath))
                {
                    if (string.IsNullOrEmpty(_currentWorkspacePath))
                    {
                        Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE GENESI]: Nessun progetto selezionato nel Nexus Explorer per salvare il file relativo: {rawPath}", Avalonia.Media.Brushes.Red));
                        continue;
                    }

                    // ========================================================
                    // PATCH ANTI-ALLUCINAZIONE (SMART ROOT MATCHER)
                    // ========================================================
                    string? matchedFile = FuzzyFindExistingFile(rawPath, _currentWorkspacePath);

                    // Caso 1: Auto-Fix. Se il file esiste già nel progetto originario
                    if (matchedFile != null && !rawPath.EndsWith(".csproj"))
                    {
                        finalPath = matchedFile; // Sovrascrive il VERO file, ignorando il path fasullo dell'IA
                    }
                    else
                    {
                        // Caso 2: Genesi da zero.
                        finalPath = Path.Combine(_currentWorkspacePath, rawPath);
                    }
                }

                parsedFiles.Add((rawPath, finalPath, codeContent));

                // Se l'IA sta generando un .csproj, significa che è una rigenerazione totale
                // if (finalPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                // {
                    // projectRootToPurge = Path.GetDirectoryName(finalPath);
                    // isFullProjectGeneration = true;
                // }
            }

            // Fallback root
            if (string.IsNullOrEmpty(projectRootToPurge) && !string.IsNullOrEmpty(_currentWorkspacePath))
            {
                projectRootToPurge = _currentWorkspacePath;
            }

            // --- FASE 2: SISTEMA DI AUTO-EPURAZIONE (SOLO PER RIGENERAZIONI TOTALI) ---
            if (isFullProjectGeneration && !string.IsNullOrEmpty(projectRootToPurge) && Directory.Exists(projectRootToPurge))
            {
                // VINCOLO DI SICUREZZA SUPREMO: Evita di epurare cartelle di sistema o la Home intera
                if (projectRootToPurge != "/" && projectRootToPurge != "/home/spiderman" && !projectRootToPurge.StartsWith("/tmp"))
                {
                    try
                    {
                        Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 🧹 Rigenerazione Totale rilevata. Inizio epurazione vecchi file in: {Path.GetFileName(projectRootToPurge)}", Brushes.Orange));
                        
                        // Aggiunto *.xaml per sicurezza
                        string[] extensionsToPurge = { "*.cs", "*.axaml", "*.xaml", "*.csproj" };
                        int deletedCount = 0;

                        // 1. Cancella i file sorgente
                        foreach (var ext in extensionsToPurge)
                        {
                            var oldFiles = Directory.GetFiles(projectRootToPurge, ext, SearchOption.AllDirectories);
                            foreach (var oldFile in oldFiles)
                            {
                                File.Delete(oldFile);
                                deletedCount++;
                            }
                        }
                        
                        // 2. Rimuove le sottocartelle rimaste vuote (es. un vecchio "ViewModels")
                        var allDirs = Directory.GetDirectories(projectRootToPurge, "*", SearchOption.AllDirectories);
                        // Ordina in modo decrescente per cancellare prima le cartelle figlie, poi le madri
                        foreach (var dir in allDirs.OrderByDescending(d => d.Length)) 
                        {
                            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                            {
                                Directory.Delete(dir);
                            }
                        }

                        if (deletedCount > 0)
                            Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 🗑️ Eliminati {deletedCount} file/fantasmi obsoleti.", Brushes.Gray));
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE EPURAZIONE]: {ex.Message}", Brushes.Red));
                    }
                }
            }
            else if (!isFullProjectGeneration)
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 🩹 Patch parziale rilevata. L'Epurazione totale è disabilitata. Verranno sovrascritti solo i file interessati.", Brushes.Orange));
            }

            // --- FASE 3: SCRITTURA DEI NUOVI FILE (GENESI) ---
            foreach (var fileToProcess in parsedFiles)
            {
                try
                {
                    string? directoryPath = Path.GetDirectoryName(fileToProcess.FinalPath);
                    
                    if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                    {
                        if (!directoryPath.StartsWith("/home/spiderman") && !directoryPath.StartsWith("/run/media") && !directoryPath.StartsWith("/tmp"))
                        {
                            Dispatcher.UIThread.Post(() => AppendToChat($"[VIOLAZIONE SICUREZZA]: Tentativo di scrittura non autorizzato in {directoryPath}", Brushes.Red));
                            continue;
                        }

                        Directory.CreateDirectory(directoryPath);
                        Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 📁 Generata directory: {directoryPath}", Brushes.Gray));
                    }

                    File.WriteAllText(fileToProcess.FinalPath, fileToProcess.Code, Encoding.UTF8);
                    
                    string fileName = Path.GetFileName(fileToProcess.FinalPath);
                    Dispatcher.UIThread.Post(() => AppendToChat($"[⚙️ AUTONOMO]: 💾 File inciso con successo: {fileToProcess.FinalPath}", Brushes.SpringGreen));
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE GENESI]: Impossibile scrivere {fileToProcess.FinalPath}. Det: {ex.Message}", Brushes.Red));
                }
            }
        }

        // --- FUNZIONE PER SCRIVERE NELLA CHAT (CON MENU CONTESTUALE COPIA) ---
        private (SelectableTextBlock textBlock, StackPanel container) AppendToChat(string fullText, IBrush color, bool showCopy = false, string copyPrefixToRemove = "")
        {
            var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
            if (chatPanel == null) return (new SelectableTextBlock(), new StackPanel());

            var mainContainer = new StackPanel { Spacing = 5, Margin = new Avalonia.Thickness(0, 5, 0, 15) };
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            var textBlock = new SelectableTextBlock { Text = fullText, Foreground = color, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            
            // --- INIZIO PATCH MENU CONTESTUALE ---
            var contextMenu = new ContextMenu();
            var copyMenuItem = new MenuItem { Header = "📋 Copia Intero Messaggio" };
            copyMenuItem.Click += async (sender, e) =>
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    // Usa la logica di pulizia già prevista se presente un prefisso
                    string textToCopy = textBlock.Text ?? "";
                    if (!string.IsNullOrEmpty(copyPrefixToRemove) && textToCopy.StartsWith(copyPrefixToRemove))
                    {
                        textToCopy = textToCopy.Substring(copyPrefixToRemove.Length).TrimStart();
                    }
                    
                    await clipboard.SetTextAsync(textToCopy);
                    
                    // Feedback visivo opzionale (cambia colore per un secondo)
                    var oldBrush = textBlock.Foreground;
                    textBlock.Foreground = Brushes.SpringGreen;
                    await Task.Delay(500);
                    textBlock.Foreground = oldBrush;
                }
            };
            contextMenu.Items.Add(copyMenuItem);
            textBlock.ContextMenu = contextMenu;
            // --- FINE PATCH MENU CONTESTUALE ---

            Grid.SetColumn(textBlock, 0); 
            rowGrid.Children.Add(textBlock);

            if (showCopy)
            {
                var copyBtn = new Button
                {
                    Content = "📋", Background = Brushes.Transparent, Foreground = Brushes.Gray,
                    Padding = new Avalonia.Thickness(5), Cursor = new Cursor(StandardCursorType.Hand),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                };
                ToolTip.SetTip(copyBtn, "Copia testo IA");
                Grid.SetColumn(copyBtn, 1);
                
                copyBtn.Click += async (s, e) =>
                {
                    if (this.Clipboard != null)
                    {
                        string textToCopy = textBlock.Text ?? "";
                        if (!string.IsNullOrEmpty(copyPrefixToRemove) && textToCopy.StartsWith(copyPrefixToRemove))
                        {
                            textToCopy = textToCopy.Substring(copyPrefixToRemove.Length).TrimStart();
                        }
                        await this.Clipboard.SetTextAsync(textToCopy);
                        copyBtn.Content = "✅";
                        await Task.Delay(2000);
                        if (copyBtn != null) copyBtn.Content = "📋";
                    }
                };
                rowGrid.Children.Add(copyBtn);
            }

            mainContainer.Children.Add(rowGrid);
            chatPanel.Children.Add(mainContainer);
            
            Dispatcher.UIThread.Post(() => { var scrollViewer = chatPanel.Parent as ScrollViewer; scrollViewer?.ScrollToEnd(); });
            return (textBlock, mainContainer);
        }

        // --- MOTORE TELEGRAM (RICEZIONE MESSAGGI E BOTTONI) ---
        private async Task HandleTelegramUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // 1. GESTIONE MESSAGGI DI TESTO TRADIZIONALI
            if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text)
            {
                string testRicevuto = update.Message.Text ?? "";
                long chatId = update.Message.Chat.Id;

                Dispatcher.UIThread.Post(async () => 
                {
                    AppendToChat($"[TELEGRAM]: {testRicevuto}", Brushes.Violet);
                    await HandleJakAssistantAsync(testRicevuto, chatId, false);
                });
                return;
            }

            // --- NUOVO: 1.5 GESTIONE MESSAGGI VOCALI TELEGRAM ---
            if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Voice)
            {
                long chatId = update.Message.Chat.Id;
                string fileId = update.Message.Voice!.FileId;

                Dispatcher.UIThread.Post(() => AppendToChat($"[TELEGRAM]: 🎤 Vocale ricevuto, download in corso...", Brushes.Violet));

                try
                {
                    var file = await botClient.GetFile(fileId);
                    string tempAudioPath = Path.Combine(Path.GetTempPath(), "telegram_voice.ogg");
                    string wavPath = Path.Combine(Path.GetTempPath(), "telegram_voice.wav");

                    using (var saveFileStream = File.Open(tempAudioPath, FileMode.Create))
                    {
                        await botClient.DownloadFile(file.FilePath!, saveFileStream);
                    }

                    Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: ⚙️ Conversione e Trascrizione in corso...", Brushes.Gray));

                    // --- INIZIO PATCH: CONVERSIONE FFMPEG + ESECUZIONE WHISPER-CLI ---
                    // 1. Conversione in WAV 16kHz mono
                    var convertPsi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-y -i \"{tempAudioPath}\" -ar 16000 -ac 1 -c:a pcm_s16le \"{wavPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (var convertProcess = System.Diagnostics.Process.Start(convertPsi)) 
                    { 
                        await convertProcess!.WaitForExitAsync(); 
                    }

                    // 2. Esecuzione Whisper-CLI
                    string whisperCliPath = "/home/spiderman/ai_models/whisper.cpp/build/bin/whisper-cli";
                    string modelPath = "/home/spiderman/ai_models/whisper-large-v3.bin";

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = whisperCliPath,
                        Arguments = $"-m {modelPath} -f {wavPath} -l it -nt", 
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = System.Diagnostics.Process.Start(psi)!;
                    string transcript = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    // --- FINE PATCH ---

                    if (!string.IsNullOrEmpty(transcript))
                    {
                        Dispatcher.UIThread.Post(async () =>
                        {
                            AppendToChat($"[TELEGRAM - Trascrizione]: {transcript.Trim()}", Brushes.Yellow);
                            await HandleJakAssistantAsync(transcript.Trim(), chatId, false);
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE VOCALE TELEGRAM]: {ex.Message}", Brushes.Red));
                }
                return;
            }

            // 2. GESTIONE CLICK SUI BOTTONI (CALLBACK QUERIES)
            if (update.Type == UpdateType.CallbackQuery)
            {
                string callbackData = update.CallbackQuery!.Data ?? "";
                long chatId = update.CallbackQuery.Message!.Chat.Id;
                await botClient.AnswerCallbackQuery(update.CallbackQuery.Id);
                Dispatcher.UIThread.Post(async () => 
                {
                    AppendToChat($"[TELEGRAM COMANDO]: {callbackData}", Brushes.Orange);
                    await HandleTelegramCommandAsync(callbackData, chatId);
                });
            }
        }

        // --- GESTORE DEGLI STATI E DEI COMANDI TELEGRAM ---
        private async Task HandleTelegramCommandAsync(string command, long chatId)
        {
            if (command == "CMD_ARCHITETTO")
            {
                if (string.IsNullOrWhiteSpace(_lastTelegramRequest))
                {
                    await _botClient!.SendMessage(chatId, "⚠️ Nessuna richiesta in memoria da delegare.");
                    return;
                }
                
                await _botClient!.SendMessage(chatId, "⚙️ Risveglio dell'Architetto in corso...");
                
                // Chiamiamo il motore estratto passando la memoria a breve termine
                await InvokeArchitectAsync(_lastTelegramRequest, chatId, false);
            }
            else if (command == "CMD_PROGETTO")
            {
                await _botClient!.SendMessage(chatId, "📂 Ottimo. Scrivimi il nome della cartella o la root del nuovo progetto:");
                // Qui implementeremo la logica per salvare il path
            }
            else if (command == "CMD_CODER")
            {
                await _botClient!.SendMessage(chatId, "⚙️ Delega al Coder (8082) in corso...\n*(Nello step successivo collegheremo il motore Coder qui!)*");
            }
        }

        private async Task HandleTelegramErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE TELEGRAM]: {exception.Message}", Brushes.Red));
            await Task.Delay(3000, cancellationToken); 
        }

        // --- MOTORE JAK (ASSISTENTE L0) ---
        private async Task HandleJakAssistantAsync(string userText, long telegramChatId, bool useVoice)
        {
            AppendToChat("[jak]: Sto elaborando la richiesta...", Brushes.LightSkyBlue);
            if (telegramChatId != 0) _lastTelegramRequest = userText; 
            
            // --- LOGICA VISION ROBUSTA ---
            object messageContent;
            if (!string.IsNullOrEmpty(_currentImagePath))
            {
                // 1. Riconoscimento dinamico del formato (PNG vs JPEG)
                string ext = Path.GetExtension(_currentImagePath).ToLower();
                string mimeType = (ext == ".png") ? "image/png" : "image/jpeg";
                string base64Image = ConvertImageToBase64(_currentImagePath);

                // 2. Uso di Dictionary per bypassare i limiti di System.Text.Json con oggetti anonimi
                // Ordine: Prima l'immagine, poi il testo (Best practice per Qwen-VL)
                messageContent = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> 
                    { 
                        { "type", "image_url" }, 
                        { "image_url", new Dictionary<string, string> { { "url", $"data:{mimeType};base64,{base64Image}" } } } 
                    },
                    new Dictionary<string, object> 
                    { 
                        { "type", "text" }, 
                        { "text", userText } 
                    }
                };

                // 3. Svuotiamo la coda visiva così non reinvia l'immagine al prossimo giro
                _currentImagePath = null;
                Dispatcher.UIThread.Post(() => {
                    var previewContainer = this.FindControl<Border>("ImagePreviewContainer");
                    if (previewContainer != null) previewContainer.IsVisible = false;
                });
            }
            else
            {
                messageContent = userText;
            }

	    ClearImagePreview();

            _jakHistory.Add(new Dictionary<string, object> { { "role", "user" }, { "content", messageContent } });
            
            var payload = new { messages = _jakHistory, temperature = 0.7, max_tokens = 2048, stream = true };
            // Collegato al demone Vision sulla 8084
            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8084/v1/chat/completions")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            try
            {
                var (aiMessageBlock, _) = AppendToChat("[jak]:\n", Brushes.DodgerBlue, true, "[jak]:\n");
                var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
                var scrollViewer = chatPanel?.Parent as ScrollViewer;

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                
                // Controllo Errori Server aggiornato
                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE SERVER 8084]: {errorContent}", Brushes.Red));
                    return;
                }
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);
                StringBuilder jakFullResponse = new StringBuilder();

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: ") && line.Substring(6) != "[DONE]")
                    {
                        var doc = JsonDocument.Parse(line.Substring(6));
                        var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement))
                        {
                            string chunk = contentElement.GetString() ?? "";
                            jakFullResponse.Append(chunk);
                            Dispatcher.UIThread.Post(() => { aiMessageBlock.Text += chunk; scrollViewer?.ScrollToEnd(); });
                        }
                    }
                }

                _jakHistory.Add(new Dictionary<string, object> { { "role", "assistant" }, { "content", jakFullResponse.ToString() } });

		// --- SALVATAGGIO ASSISTENTE JAK SU DISCO ---
		_currentSession.Messages.Add(new ChatMessage { Role = "assistant", Content = jakFullResponse.ToString() });
		_sessionManager.SaveSession(_currentSession, _currentWorkspacePath);
		
                if (useVoice) await SpeakAsync(jakFullResponse.ToString());
                
                if (telegramChatId != 0 && _botClient != null)
                {
                    await _botClient.SendMessage(chatId: telegramChatId, text: jakFullResponse.ToString());
                }
            }
            catch (Exception ex) { AppendToChat($"[ERRORE JAK]: {ex.Message}", Brushes.Red); }
        }

	private string ConvertImageToBase64(string imagePath)
        {
            try 
            {
                byte[] imageBytes = File.ReadAllBytes(imagePath);
                return Convert.ToBase64String(imageBytes);
            }
            catch { return ""; }
        }

	private void AttachImageFromPath(string path)
        {
            _currentImagePath = path;
            var previewContainer = this.FindControl<Border>("ImagePreviewContainer");
            var imageNameText = this.FindControl<TextBlock>("AttachedImageName");
            
            // Peschiamo il nuovo riquadro immagine dall'interfaccia
            var imagePreview = this.FindControl<Avalonia.Controls.Image>("AttachedImagePreview");

            if (previewContainer != null && imageNameText != null)
            {
                imageNameText.Text = Path.GetFileName(_currentImagePath);
                
                // MAGIA VISIVA: Carichiamo fisicamente l'immagine nell'anteprima
                if (imagePreview != null)
                {
                    try
                    {
                        // Legge il file dal disco e lo trasforma in una miniatura visibile
                        imagePreview.Source = new Bitmap(_currentImagePath);
                    }
                    catch { /* Se l'immagine è corrotta, ignoriamo e mostriamo solo il nome */ }
                }

                previewContainer.IsVisible = true;
            }
            AppendToChat($"[SISTEMA]: 📎 Immagine caricata in coda visiva: {Path.GetFileName(_currentImagePath)}", Brushes.LightSkyBlue);
        }

        private void ClearImagePreview()
        {
            _currentImagePath = null;
            Dispatcher.UIThread.Post(() => {
                var previewContainer = this.FindControl<Border>("ImagePreviewContainer");
                if (previewContainer != null) previewContainer.IsVisible = false;
                
                // Svuotiamo il riquadro immagine per liberare memoria
                var imagePreview = this.FindControl<Avalonia.Controls.Image>("AttachedImagePreview");
                if (imagePreview != null) imagePreview.Source = null; 
            });
        }

	private async void OnUserInputTextBoxKeyDown(object? sender, KeyEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            // 1. GESTIONE INVIO (Send) vs SHIFT+INVIO (A capo)
            if (e.Key == Key.Enter || e.Key == Key.Return)
            {
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
                else
                {
                    e.Handled = true;
                    OnSendButtonClicked(this.FindControl<Button>("SendButton"), new RoutedEventArgs());
                    return;
                }
            }

            // 📸 2. GESTIONE CTRL+V PER IMMAGINI CRUDE (Tasto Stamp)
            // Intercettiamo qui perché il TextBox ignora il comando se non c'è testo!
            if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    string? pasteText = await clipboard.TryGetTextAsync();
                    // Se non c'è testo negli appunti, è altamente probabile sia un'immagine raw
                    if (string.IsNullOrEmpty(pasteText))
                    {
                        e.Handled = true;
                        await TryPasteRawImageAsync();
                    }
                }
            }
        }

	// --- GESTIONE UNIVERSALE INCOLLA ---
        private async void OnTextBoxPastingFromClipboard(object? sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox == null) return;

            e.Handled = true; // Blocchiamo il sistema operativo

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null) return;

            string? pasteText = await clipboard.TryGetTextAsync();
            
            // 📸 NOVITÀ: Se non c'è testo, non ignoriamo più! 
            // Lanciamo il recupero dell'immagine cruda anche se hai usato il mouse.
            if (string.IsNullOrEmpty(pasteText))
            {
                await TryPasteRawImageAsync();
                return; 
            }

            string cleanPath = pasteText.Trim().Replace("file://", "");
            
            if (File.Exists(cleanPath))
            {
                string ext = Path.GetExtension(cleanPath).ToLower();
                
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                {
                    AttachImageFromPath(cleanPath);
                    return; 
                }
                else if (ext == ".cs" || ext == ".txt" || ext == ".json" || ext == ".md" || ext == ".sh" || ext == ".py" || ext == ".xml")
                {
                    try
                    {
                        string fileContent = await File.ReadAllTextAsync(cleanPath);
                        string formattedText = $"\n\nEcco il contenuto del file `{Path.GetFileName(cleanPath)}`:\n```{ext.Replace(".", "")}\n{fileContent}\n```\n";
                        Dispatcher.UIThread.Post(() => InsertTextAtCaret(textBox, formattedText));
                        return;
                    }
                    catch { return; }
                }
            }

            // Testo NORMALE
            Dispatcher.UIThread.Post(() => InsertTextAtCaret(textBox, pasteText));
        }

        // Funzione chirurgica per incollare il testo esattamente dove si trova il cursore
        private void InsertTextAtCaret(TextBox textBox, string textToInsert)
        {
            if (textBox.SelectionStart != textBox.SelectionEnd)
            {
                int start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
                int length = Math.Abs(textBox.SelectionStart - textBox.SelectionEnd);
                string currentText = textBox.Text ?? "";
                textBox.Text = currentText.Remove(start, length).Insert(start, textToInsert);
                textBox.CaretIndex = start + textToInsert.Length;
            }
            else
            {
                int caretIndex = Math.Max(0, textBox.CaretIndex);
                string currentText = textBox.Text ?? "";
                textBox.Text = currentText.Insert(caretIndex, textToInsert);
                textBox.CaretIndex = caretIndex + textToInsert.Length;
            }
        }

	// --- MOTORE ESTRAZIONE PIXEL DA LINUX ---
        private async Task TryPasteRawImageAsync()
        {
            string tempScreenPath = Path.Combine(Path.GetTempPath(), "nexus_raw_screenshot.png");
            
            try
            {
                if (File.Exists(tempScreenPath)) File.Delete(tempScreenPath);

                // Chiediamo a Linux di scaricare i pixel in un file PNG temporaneo
                string bashCmd = $"xclip -selection clipboard -t image/png -o > '{tempScreenPath}' 2>/dev/null || wl-paste --type image/png > '{tempScreenPath}' 2>/dev/null";
                
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{bashCmd}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null) await proc.WaitForExitAsync();

                FileInfo fi = new FileInfo(tempScreenPath);
                if (fi.Exists && fi.Length > 0)
                {
                    AttachImageFromPath(tempScreenPath);
                }
                else
                {
                    Dispatcher.UIThread.Post(() => AppendToChat("[SISTEMA]: Immagine non trovata negli appunti o pacchetto mancante. Esegui 'sudo pacman -S xclip' su Manjaro.", Brushes.Orange));
                }
            }
            catch (Exception ex)
            {
                 Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE CLIPBOARD]: {ex.Message}", Brushes.Red));
            }
        }

	// --- MOTORE AVVIO CONTAINER (DYNAMIC JSON BOOTSTRAP) ---
        private async Task BootSystemModeAsync(string mode)
        {
            // Nascondiamo i bottoni e mostriamo la barra di caricamento
            var btnIde = this.FindControl<Button>("BootIdeModeButton");
            var btnHacker = this.FindControl<Button>("BootHackerModeButton");
            
            if (btnIde != null) btnIde.IsVisible = false;
            if (btnHacker != null) btnHacker.IsVisible = false;
            
            var progress = this.FindControl<ProgressBar>("StartupProgressBar");
            var status = this.FindControl<TextBlock>("StartupStatusText");
            
            if (progress != null) progress.IsVisible = true;
            if (status != null) status.IsVisible = true;

            // Legge il percorso principale dal JSON
            string storagePath = _configManager.CurrentConfig.StoragePath;

            // --- LETTURA DINAMICA DAL JSON ---
            if (_configManager.CurrentConfig.Modes.TryGetValue(mode, out var modelsToLoad))
            {
                int totalModels = modelsToLoad.Count;
                int currentStep = 0;

                foreach (var model in modelsToLoad)
                {
                    // --- SMART BOOT ARCHITECTURE ---
                    // Se lo Smart Hot-Swapping è attivo, saltiamo il Coder al boot. 
                    // Se l'operatore lo ha disattivato (perché ha 112GB di VRAM e vuole tutto pronto), lo carichiamo subito!
                    if (model.Id == "Coder_Principale" && _configManager.CurrentConfig.HotSwapEnabled) 
                    {
                        Console.WriteLine($"[SISTEMA] {model.Id} ignorato al boot (Smart Hot-Swapping attivo).");
                        continue;
                    }
                    // ---------------------------------------------------------------------------------

                    if (status != null) status.Text = $"Avvio {model.Id}... [Porta {model.Port}]";
                    
                    if (!string.IsNullOrEmpty(model.MmprojFileName))
                    {
                        // Se c'è il file mmproj, è un modello visivo (es. Jak L0)
                        await _containerManager.StartContainerAsync(model.Id, $"{storagePath}/{model.FileName}", model.Port, model.ContextSize, $"{storagePath}/{model.MmprojFileName}");
                    }
                    else
                    {
                        // Avvio standard per tutti gli altri
                        await _containerManager.StartContainerAsync(model.Id, $"{storagePath}/{model.FileName}", model.Port, model.ContextSize);
                    }
                    
                    currentStep++;
                    if (progress != null) progress.Value = (currentStep * 100) / totalModels;
                }
            }
            
            await Task.Delay(1000); // Pausa di stabilizzazione hardware
            
            // Nascondiamo l'intero pannello nero e mostriamo l'IDE
            var splash = this.FindControl<Border>("SplashScreenOverlay");
            if (splash != null) splash.IsVisible = false;
            
            // Adattamento UI dinamico (Accademia o Sviluppo)
            AdaptUIForMode(mode);
            
            AppendToChat($"[SISTEMA]: Infrastruttura {mode} avviata e stabilizzata.", Brushes.LightGreen);
        }

	// --- MOTORE DIRETTIVO ACCADEMIA (AUTO-APPRENDIMENTO) ---
        private async Task HandleTrainingModeAsync(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                AppendToChat("[SISTEMA]: ⚠️ Specifica un argomento. Esempio: /addestra Sicurezza Reti WPA3", Brushes.Orange);
                return;
            }

            AppendToChat($"[SISTEMA]: 👨‍🏫 Contatto il Direttore Didattico (Master Mentor) per stilare il piano di studi su: '{topic}'...", Brushes.LightSkyBlue);

            // Chiediamo a Gemma 3 di comportarsi da Direttore e stilare le chiavi di ricerca
            string prompt = $"L'utente vuole addestrare il sistema sull'argomento: '{topic}'. Tu sei il Direttore Didattico. Genera una lista di massimo 5 chiavi di ricerca web altamente tecniche (in inglese per risultati migliori) che un web crawler userà per scaricare manuali, repository e paper. RISPONDI SOLO CON UN ARRAY JSON DI STRINGHE, senza altro testo. Esempio: [\"query 1\", \"query 2\"]";

            var payload = new {
                messages = new[] {
                    new { role = "system", content = "Sei un JSON generator. Rispondi SOLO con un array JSON valido." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.3, max_tokens = 500, stream = false 
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8081/v1/chat/completions") {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            try
            {
                using var response = await _httpClient.SendAsync(request);
                string jsonResult = await response.Content.ReadAsStringAsync();
                
                using var doc = JsonDocument.Parse(jsonResult);
                string aiJsonString = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "[]";
                
                // Pulizia nel caso il modello aggiunga markdown come ```json ... ```
                aiJsonString = Regex.Replace(aiJsonString, @"```json\s*", "");
                aiJsonString = Regex.Replace(aiJsonString, @"```\s*", "").Trim();

                List<string>? queries = JsonSerializer.Deserialize<List<string>>(aiJsonString);
                
                if (queries != null && queries.Count > 0)
                {
                    AppendToChat($"[DIRETTORE DIDATTICO]: Piano di studi approvato. Avvio il Ragno su {queries.Count} direttrici di ricerca...", Brushes.Gold);
                    
                    // Lanciamo il Ragno in background senza bloccare la GUI!
                    // Gli diciamo di scavare 3 pagine per ogni query (per un totale di ~15 fonti)
                    _ = Task.Run(async () => 
                    {
                        await _crawler.StartTrainingAsync(topic, queries, 3, (logMsg) => 
                        {
                            Dispatcher.UIThread.Post(() => AppendToChat(logMsg, Brushes.MediumPurple));
                        });
                    });
                }
                else
                {
                    AppendToChat("[ERRORE ACCADEMIA]: Il Master Mentor non ha generato un piano valido.", Brushes.Red);
                }
            }
            catch (Exception ex)
            {
                AppendToChat($"[ERRORE ACCADEMIA]: {ex.Message}", Brushes.Red);
            }
        }

	// =========================================================================
        // SPEGNIMENTO SICURO DELL'INFRASTRUTTURA
        // =========================================================================
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            AppendToChat("[SISTEMA]: Spegnimento reattori in corso...", Brushes.Orange);

            // 1. Chiede al manager di terminare i processi tracciati dolcemente
            _containerManager.KillAllContainers();
            
            // 2. Pulizia di sicurezza brutale a livello Linux per evitare processi fantasma
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = "-c \"killall -9 llama-server\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

	// --- INTERFACCIA DI ADDESTRAMENTO CON COGNIZIONE DEI FALLIMENTI ---
        private async Task ArchiveSuccessKnowledgeAsync(string code)
        {
            try {
                string label = $"[SUCCESSO_LOGICA] [{DateTime.Now:yyyyMMdd}]";
                await _vectorMemory.MemorizeContentAsync(label, $"[PATTERN VALIDO COMPILATO]:\n{code}");
            } catch { }
        }

        private async Task ArchiveFailureConscienceAsync(string badCode, string critique)
        {
            try {
                string label = $"[COSCIENZA_FALLIMENTO] [{DateTime.Now:yyyyMMdd}]";
                string content = $"[ANTI-PATTERN DA EVITARE COMPLETAMENTE]:\n{badCode}\n\n[CRITICA DEL TRIBUNALE]:\n{critique}";
                await _vectorMemory.MemorizeContentAsync(label, content);
                Dispatcher.UIThread.Post(() => AppendToChat("[⚙️ COSCIENZA]: Archiviata firma del bug nella memoria dei fallimenti.", Brushes.Tomato));
            } catch { }
        }

	// =========================================================================
        // MOTORE UI DELLA MEMORIA STORICA (SPLITVIEW & SESSIONI)
        // =========================================================================

        private void InitializeSidebarEvents()
        {
            var toggleBtn = this.FindControl<Button>("ToggleSidebarButton");
            if (toggleBtn != null) toggleBtn.Click += (s, e) => 
            {
                var splitView = this.FindControl<SplitView>("ChatSplitView");
                if (splitView != null) splitView.IsPaneOpen = !splitView.IsPaneOpen;
                RefreshChatHistoryUI();
            };

            var newChatBtn = this.FindControl<Button>("NewChatButton");
            if (newChatBtn != null) newChatBtn.Click += (s, e) => StartNewChatSession();

            var chatList = this.FindControl<ListBox>("ChatHistoryListBox");
            if (chatList != null) chatList.SelectionChanged += OnChatSelectedFromList;
        }

        private void RefreshChatHistoryUI()
        {
            var chatList = this.FindControl<ListBox>("ChatHistoryListBox");
            if (chatList != null)
            {
                // Peschiamo le chat aggiornate dal disco (generiche + quelle del progetto se aperto)
                var sessions = _sessionManager.LoadAllAvailableSessions(_currentWorkspacePath);
                
                // Formattazione visiva dei titoli per i fissati
                foreach (var session in sessions)
                {
                    if (session.IsPinned && !session.Title.StartsWith("📌"))
                        session.Title = "📌 " + session.Title;
                    else if (!session.IsPinned && session.Title.StartsWith("📌 "))
                        session.Title = session.Title.Substring(3);
                }
                
                chatList.ItemsSource = sessions;
            }
        }

        private void StartNewChatSession()
        {
            // Svuotiamo la RAM
            _currentSession = new ChatSession { Title = $"Chat del {DateTime.Now:dd/MM HH:mm}" };
            _chatHistory.Clear();
            _chatHistory.Add(new Dictionary<string, string> { { "role", "system" }, { "content", GetDynamicSystemPrompt() } });
            
            // Svuotiamo lo schermo visivo
            var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
            if (chatPanel != null) chatPanel.Children.Clear();

            AppendToChat("[SISTEMA]: 📄 Nuova sessione avviata. L'ambiente è pronto.", Brushes.LightGreen);
            RefreshChatHistoryUI();
        }

	// --- AUTO RINOMINA INTELLIGENTE IN BACKGROUND ---
        private async Task AutoRenameSessionAsync()
        {
            try
            {
                string prompt = "Analizza questa breve conversazione iniziale. Genera un titolo di massimo 3 o 4 parole chiave (es. 'Interfaccia Player Audio', 'Logica Server Telegram'). Rispondi SOLO con il titolo, senza virgolette e senza punteggiatura finale.";
                
                var tempHistory = new List<Dictionary<string, string>>(_chatHistory);
                tempHistory.Add(new Dictionary<string, string> { { "role", "user" }, { "content", prompt } });
                
                var payload = new { messages = tempHistory, temperature = 0.3, max_tokens = 15 };
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8081/v1/chat/completions") {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode) {
                    string jsonResult = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(jsonResult);
                    string newTitle = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "";
                    
                    if (!string.IsNullOrEmpty(newTitle))
                    {
                        _sessionManager.RenameSession(_currentSession, newTitle);
                        // Aggiorniamo la UI grafica per farti vedere il titolo che cambia da solo
                        Dispatcher.UIThread.Post(() => RefreshChatHistoryUI());
                    }
                }
            } catch { } // Se fallisce ignora in silenzio per non disturbare l'utente
        }

        private void OnChatSelectedFromList(object? sender, SelectionChangedEventArgs e)
        {
            var chatList = sender as ListBox;
            if (chatList?.SelectedItem is ChatSession selectedSession)
            {
                // Disabilitiamo temporaneamente l'evento per non fare loop
                chatList.SelectionChanged -= OnChatSelectedFromList;
                
                _currentSession = selectedSession;
                _chatHistory.Clear();
                _chatHistory.Add(new Dictionary<string, string> { { "role", "system" }, { "content", GetDynamicSystemPrompt() } });

                // Svuotiamo e ristampiamo a schermo tutta la cronologia passata
                var chatPanel = this.FindControl<StackPanel>("ChatLogPanel");
                if (chatPanel != null) chatPanel.Children.Clear();

                AppendToChat($"[SISTEMA]: 📂 Caricamento sessione: {_currentSession.Title}", Brushes.Gray);

                foreach (var msg in _currentSession.Messages)
                {
                    _chatHistory.Add(new Dictionary<string, string> { { "role", msg.Role ?? "user" }, { "content", msg.Content ?? "" } });
                    
                    if (msg.Role == "user")
                        AppendToChat($"[EMANUELE]: {msg.Content}", Brushes.White);
                    else
                        AppendToChat($"[MASTER MENTOR]:\n{msg.Content}", Brushes.LightGreen);
                }

                chatList.SelectedItem = null; // Deseleziona per permettere riclick
                chatList.SelectionChanged += OnChatSelectedFromList;
                
                var splitView = this.FindControl<SplitView>("ChatSplitView");
                if (splitView != null) splitView.IsPaneOpen = false; // Chiude il cassetto automaticamente
            }
        }

        // --- AZIONI DEL MENU A TENDINA (I TRE PUNTINI) ---
        private void OnDeleteChatClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ChatSession session)
            {
                _sessionManager.DeleteSession(session);
                if (_currentSession.Id == session.Id) StartNewChatSession(); // Se ho eliminato quella attiva, pulisco lo schermo
                RefreshChatHistoryUI();
            }
        }

        private void OnTogglePinChatClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ChatSession session)
            {
                _sessionManager.TogglePinSession(session);
                RefreshChatHistoryUI();
            }
        }

        private async void OnRenameChatClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is ChatSession session)
            {
                // Invia alla IA l'ordine in background di riassumere la chat per generare un titolo!
                if (session.Messages.Count > 0)
                {
                    AppendToChat("[SISTEMA]: 🤖 L'IA sta leggendo la chat per generare un titolo appropriato...", Brushes.Gray);
                    string prompt = "Riassumi il contenuto di questa nostra conversazione in un titolo breve di massimo 4 parole. Rispondi SOLO con il titolo, senza virgolette e senza spiegazioni.";
                    
                    // Clona temporaneamente la storia per non sporcare la conversazione
                    var tempHistory = new List<Dictionary<string, string>>(_chatHistory);
                    tempHistory.Add(new Dictionary<string, string> { { "role", "user" }, { "content", prompt } });
                    
                    var payload = new { messages = tempHistory, temperature = 0.3, max_tokens = 50 };
                    var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8081/v1/chat/completions") {
                        Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                    };

                    try {
                        var response = await _httpClient.SendAsync(request);
                        if (response.IsSuccessStatusCode) {
                            string jsonResult = await response.Content.ReadAsStringAsync();
                            var doc = JsonDocument.Parse(jsonResult);
                            string newTitle = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim() ?? "Chat Senza Titolo";
                            
                            _sessionManager.RenameSession(session, newTitle);
                            RefreshChatHistoryUI();
                        }
                    } catch { }
                }
            }
        }

	// --- MOTORE COSCIENZA DI PROGETTO E PERSONA STACKING ---
        private string GetDynamicSystemPrompt()
        {
            string basePrompt = @"Sei il Master Mentor, l'Orchestratore e Senior Architect AI di 'Opera Suprema' (IDE locale). 
Sei il partner di sviluppo e Co-Founder di Emanuele.

REGOLE COMPORTAMENTALI INVIOLABILI:
1. PROATTIVITÀ: Non limitarti a rispondere alla singola domanda. Proponi sempre il passo logico successivo.
2. EMPATIA E SUPPORTO: Incoraggia Emanuele se ci sono errori, analizzate gli ostacoli insieme.
3. STILE: Usa un tono confidenziale ma altamente tecnico ('da Architetto ad Architetto'). Rispondi in italiano.
4. PREPARAZIONE PER IL CODER (JSON PATCH): Quando la tua analisi richiede di modificare o generare codice, NON scriverlo tu in C#. Spiega brevemente il ragionamento a Emanuele e proponi SEMPRE un blocco JSON direttivo per il Coder.
FORMATO TASSATIVO DEL BLOCCO:
```json
{
  ""FilesToModify"": [""file1.cs"", ""file2.axaml""],
  ""ArchitecturalDirectives"": ""Spiegazione tecnica, pattern da usare e vincoli di sistema"",
  ""LogicToApply"": ""Logica esatta dei metodi da implementare, riga per riga""
}
5. VINCOLI ARCHITETTURALI SILENZIOSI: Quando l'utente ti chiede di progettare un'applicazione, DEVI applicare sempre e silenziosamente questi vincoli:
   - IMPONI SEMPRE al Coder di usare <TargetFramework>net10.0</TargetFramework>.
   - IMPONI SEMPRE al Coder di usare ESCLUSIVAMENTE i pacchetti 'Avalonia', 'Avalonia.Desktop', 'Avalonia.Themes.Fluent' imponendo la Version='11.1.0'. Niente pacchetti extra inventati.
   - IMPONI SEMPRE al Coder di generare per intero i file di avvio essenziali (Program.cs, App.axaml, App.axaml.cs) affinché l'app non vada in crash.
   - Usa sintassi XAML moderna di Avalonia 11 (usa RowDefinitions, non Rows. Usa RequestedThemeVariant per il Dark Mode).
   - REGOLA UI AVALONIA: Assicurati sempre che il costruttore della finestra chiami InitializeComponent(); prima di qualsiasi manipolazione, binding o assegnazione di eventi sui controlli UI. IMPONI TASSATIVAMENTE al Coder di recuperare i controlli UI nel costruttore usando esclusivamente il pattern this.FindControl<T>(""NomeControllo"") prima di agganciare gli eventi, altrimenti l'app andrà in NullReferenceException.
   - REGOLA ANTI-PIGRIZIA: Imponi SEMPRE al Coder di restituire i file sorgente PER INTERO, dalla prima all'ultima riga, senza mai troncarli omettendo il codice. Imponi di non dimenticare MAI l'attributo 'x:Class' nei file .axaml.
   - Usa sempre il pattern MVC (Model-View-Controller).
   Non comunicare queste regole all'utente, inseriscile direttamente e in modo imperativo nella progettazione che passerai al Coder.";

            bool hasPersona = false;

            var chkIngegnere = this.FindControl<CheckBox>("ChkPersonaIngegnere");
            if (chkIngegnere?.IsChecked == true) {
                basePrompt += @"
- [SENIOR ARCHITECT]: Sei il Lead Software Architect di Opera Suprema. Il tuo compito NON è scrivere codice, ma progettare software di livello Enterprise insieme all'utente.
LE TUE REGOLE D'ORO:
1. MAI SCRIVERE CODICE IMPLEMENTATIVO. Non scrivere mai script, classi o funzioni complete. Il codice lo scriverà il 'Coder' in una fase successiva.
2. SPIEGA LA LOGICA: Analizza le richieste dell'utente e scrivi la struttura del progetto, l'architettura (es. pattern SOLID, MVC) e la roadmap.
3. LAVORA A BLOCCHI: Progetta una cosa alla volta. Esempio: prima si progetta la GUI, la si approva, e solo dopo si pensa alla logica dei bottoni.
4. GUIDA L'UTENTE: Alla fine di ogni tua risposta, fai un riepilogo e offri sempre 2 o 3 opzioni chiare all'utente su quale debba essere il prossimo passo da affrontare (es. 'A: Iniziamo a definire la GUI. B: Definiamo prima il database. C: Vuoi modificare l'architettura?').
Attendi sempre la decisione dell'utente prima di procedere.
";
                hasPersona = true;
            }

            var chkAvvocato = this.FindControl<CheckBox>("ChkPersonaAvvocato");
            if (chkAvvocato?.IsChecked == true) {
                basePrompt += "- [AVVOCATO / BUROCRATE]: Sei un esperto legale, normativo e amministrativo. Quando analizzi documenti legali o scrivi lettere formali/diffide, usa un linguaggio giuridico inattaccabile, cita gli articoli di legge e inserisci sempre scadenze perentorie (es. '15 giorni per il riscontro').\n";
                hasPersona = true;
            }

            var chkCyber = this.FindControl<CheckBox>("ChkPersonaCybersec");
            if (chkCyber?.IsChecked == true) {
                basePrompt += "- [CYBERSECURITY EXPERT]: Sei un analista di sicurezza informatica. Analizza ogni richiesta cercando vulnerabilità (OWASP), proponendo crittografia e approcci 'zero trust'.\n";
                hasPersona = true;
            }

            if (!hasPersona) {
                basePrompt += "- [ASSISTENTE GENERALE]: Rispondi in modo utile, conciso e versatile a qualsiasi richiesta.\n";
            }

            basePrompt += "====================================\n";

            if (!string.IsNullOrEmpty(_currentWorkspacePath))
            {
                string nexusDir = Path.Combine(_currentWorkspacePath, ".nexus");
                string blueprintPath = Path.Combine(nexusDir, "blueprint.md");
                
                if (File.Exists(blueprintPath))
                {
                    basePrompt += $"\n=== VISIONE DEL PROGETTO (BLUEPRINT) ===\n{File.ReadAllText(blueprintPath)}\n========================================\n";
                }

                // Iniezione degli Stati JSON (La vera memoria persistente!)
                string memoryDir = Path.Combine(nexusDir, "memory");
                if (Directory.Exists(memoryDir))
                {
                    basePrompt += "\n=== MEMORIA DI STATO DEL PROGETTO ===\n";
                    foreach(string jsonFile in Directory.GetFiles(memoryDir, "*.json"))
                    {
                        basePrompt += $"[{Path.GetFileName(jsonFile)}]:\n{File.ReadAllText(jsonFile)}\n\n";
                    }
                    basePrompt += "Usa SEMPRE queste informazioni JSON per contestualizzare le tue decisioni. Questa è la tua VERA memoria.\n=====================================\n";
                }
            }

            // --- FIX ANTI-ALLUCINAZIONE: INIEZIONE FINALE TASSATIVA ---
            basePrompt += "\nATTENZIONE: È ASSOLUTAMENTE VIETATO GENERARE LISTE INFINITE DI PATTERN O RIPETERE LA STESSA FRASE. COME TUA ULTIMA AZIONE, DEVI OBBLIGATORIAMENTE GENERARE IL BLOCCO ```json CON LE DIRETTIVE (FilesToModify, ArchitecturalDirectives, LogicToApply) SEGUITO DAL TAG [GENERA_CODICE]. NON IGNORARE QUESTA REGOLA.";

            return basePrompt;
        }

        // Questo evento scatta ogni volta che clicchi su una CheckBox nel menu laterale!
        private void OnPersonaChanged(object? sender, RoutedEventArgs e)
        {
            // Aggiorna a caldo il cervello del Mentor nella chat attiva senza bisogno di riavviare
            if (_chatHistory.Count > 0 && _chatHistory[0]["role"] == "system")
            {
                _chatHistory[0]["content"] = GetDynamicSystemPrompt();
                
                // Opzionale: Commentalo se ritieni che scriva troppi messaggi a schermo
                AppendToChat("[SISTEMA]: 🎭 Matrice Comportamentale ri-allineata. Il Master Mentor ha acquisito le nuove direttive.", Avalonia.Media.Brushes.MediumPurple);
            }
        }

	// --- MOTORE SCOLLEGAMENTO WORKSPACE ---
        private void OnCloseProjectButtonClicked(object? sender, RoutedEventArgs e)
        {
            // Svuota la memoria del percorso
            _currentWorkspacePath = null;
            
            // Nasconde i bottoni specifici del progetto
            var blueprintBtn = this.FindControl<Button>("OpenBlueprintButton");
            if (blueprintBtn != null) blueprintBtn.IsVisible = false;

            var closeBtn = this.FindControl<Button>("CloseProjectButton");
            if (closeBtn != null) closeBtn.IsVisible = false;

            var indexBtn = this.FindControl<Button>("IndexButton");
            if (indexBtn != null) indexBtn.IsEnabled = false;

            // Ripristina l'interfaccia di RAG e Contesto
            var statusText = this.FindControl<TextBlock>("ContextStatusText");
            if (statusText != null) statusText.Text = "Database in attesa...";

            var progressBar = this.FindControl<ProgressBar>("ContextProgressBar");
            if (progressBar != null) progressBar.Value = 0;

            var fileList = this.FindControl<ListBox>("ActiveContextFilesListBox");
            if (fileList != null) fileList.ItemsSource = null;

            var selector = this.FindControl<ComboBox>("ProjectSelector");
            if (selector != null) selector.SelectedItem = null;

            // Riavvia una chat neutrale pulita
            StartNewChatSession();
            AppendToChat("[SISTEMA]: 🔌 Directory smontata. Blueprint disattivato. L'ambiente è tornato neutrale.", Brushes.Orange);
        }

	// --- MOTORE ADATTAMENTO INTERFACCIA (ACCADEMIA vs SVILUPPO) ---
        private void AdaptUIForMode(string mode)
        {
            Dispatcher.UIThread.Post(() => 
            {
                var browseBtn = this.FindControl<Button>("BrowseButton");
                var indexBtn = this.FindControl<Button>("IndexButton");
                var nexusCombo = this.FindControl<ComboBox>("ProjectSelector");
                var blueprintBtn = this.FindControl<Button>("OpenBlueprintButton");
                var closeBtn = this.FindControl<Button>("CloseProjectButton");
                
                // Peschiamo il menu a tendina
                var modeSelector = this.FindControl<ComboBox>("ModeSelector"); 

                if (mode == "ACCADEMIA")
                {
                    // In Accademia spegniamo tutto il blocco progetti
                    if (browseBtn != null) browseBtn.IsVisible = false;
                    if (indexBtn != null) indexBtn.IsVisible = false;
                    if (nexusCombo != null) nexusCombo.IsVisible = false;
                    if (blueprintBtn != null) blueprintBtn.IsVisible = false;
                    if (closeBtn != null) closeBtn.IsVisible = false;
                    
                    if (modeSelector != null) 
                    {
                        modeSelector.IsVisible = false; // Lo nascondiamo visivamente
                        modeSelector.SelectedIndex = 1; // <-- PATCH: FORZIAMO LA LOGICA SU RICERCA (Niente Coder)
                    }
                    
                    AppendToChat("[SISTEMA]: 🎓 Modalità Accademia attivata. Funzioni di sviluppo disabilitate. Ricerca Pura operativa.", Brushes.Gold);
                }
                else if (mode == "HACKER")
                {
                    // In Sviluppo accendiamo il blocco progetti
                    if (browseBtn != null) browseBtn.IsVisible = true;
                    if (indexBtn != null) indexBtn.IsVisible = true;
                    if (nexusCombo != null) nexusCombo.IsVisible = true;
                    
                    if (modeSelector != null) 
                    {
                        modeSelector.IsVisible = true; // Lo rendiamo visibile
                        modeSelector.SelectedIndex = 0; // <-- PATCH: FORZIAMO LA LOGICA SU SVILUPPO
                    }
                    
                    AppendToChat("[SISTEMA]: 💻 Modalità Sviluppo (Hacker) attivata. Nexus Explorer e Team Operativi in linea.", Brushes.SpringGreen);
                }
            });
        }

	// --- MOTORE SWAP A CALDO (CAMBIO INFRASTRUTTURA) ---
        private void OnSwapInfrastructureClicked(object? sender, RoutedEventArgs e)
        {
            Dispatcher.UIThread.Post(() => 
            {
                AppendToChat("[SISTEMA]: 🔄 Spegnimento reattori in corso. Svuotamento VRAM...", Brushes.Orange);
                
                // 1. Uccide tutti i modelli attivi
                _containerManager.KillAllContainers();
                
                // 2. Mostra di nuovo lo Splash Screen nero
                var splash = this.FindControl<Border>("SplashScreenOverlay");
                if (splash != null) splash.IsVisible = true;
                
                // 3. Riaccende i bottoni di selezione
                var btnIde = this.FindControl<Button>("BootIdeModeButton");
                var btnHacker = this.FindControl<Button>("BootHackerModeButton");
                if (btnIde != null) btnIde.IsVisible = true;
                if (btnHacker != null) btnHacker.IsVisible = true;
                
                // 4. Nasconde la barra di caricamento del precedente avvio
                var progress = this.FindControl<ProgressBar>("StartupProgressBar");
                var status = this.FindControl<TextBlock>("StartupStatusText");
                if (progress != null) progress.IsVisible = false;
                if (status != null) status.IsVisible = false;
            });
        }

	// =========================================================================
        // MOTORE DI AUTO-COMPILAZIONE E SELF-HEALING (GIUDICE PRATICO)
        // =========================================================================
        private async Task RunAutoCompilationLoopAsync(int maxRetries = 3)
        {
            if (string.IsNullOrEmpty(_currentWorkspacePath))
            {
                Dispatcher.UIThread.Post(() => AppendToChat("[SISTEMA]: ❌ Errore: Nessun progetto caricato per la compilazione.", Avalonia.Media.Brushes.Red));
                return;
            }

            int attempt = 1;
            bool compilationSuccess = false;

            // --- MEMORIA PER L'ESCALATION ---
            string lastFatalError = "";
            string lastFatalSourceContext = "";

            while (attempt <= maxRetries && !compilationSuccess)
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: ⚙️ Avvio 'dotnet build' in background (Tentativo {attempt}/{maxRetries})...", Avalonia.Media.Brushes.LightSkyBlue));

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "build",
                    WorkingDirectory = _currentWorkspacePath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                string buildOutput = "";
                string buildError = "";
                int exitCode = -1;

                try
                {
                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process != null)
                    {
                        buildOutput = await process.StandardOutput.ReadToEndAsync();
                        buildError = await process.StandardError.ReadToEndAsync();
                        await process.WaitForExitAsync();
                        exitCode = process.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE DI SISTEMA]: Impossibile lanciare dotnet. {ex.Message}", Avalonia.Media.Brushes.Red));
                    return;
                }

                if (exitCode == 0)
                {
                    compilationSuccess = true;
                    Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: ✅ ZERO ERRORI! Compilazione superata con successo.", Avalonia.Media.Brushes.SpringGreen));
                    
                    // --- SOSTITUZIONE: AVVIO TRAMITE SUPERVISORE ---
                    // Eseguiamo il Supervisore in background per non bloccare l'IDE mentre l'app gira e restiamo in ascolto dei crash
                    _ = Task.Run(async () => await ExecuteAndSuperviseAppAsync());
                    // -----------------------------------------------
                    
                    break;
                }
                else
                {
                    // Uniamo output ed errore e filtriamo SOLO le righe che contengono veri errori (CS per C#, AVLN per Avalonia)
                    string fullErrorLog = buildOutput + "\n" + buildError;
                    var errorLines = fullErrorLog.Split('\n').Where(l => l.Contains("error CS") || l.Contains("error AVLN") || l.Contains("La compilazione non è riuscita")).ToList();
                    string cleanErrorLog = string.Join("\n", errorLines);

                    if (string.IsNullOrWhiteSpace(cleanErrorLog)) cleanErrorLog = "Errore generico di compilazione: \n" + buildError;

                    // --- VERSIONE CORRETTA: SCRITTURA NELLA MEMORIA A LUNGO TERMINE (ERROR LEDGER CON SELF-HEALING) --- [PATCH APPLICATA]
                    if (!string.IsNullOrEmpty(_currentWorkspacePath))
                    {
                        string ledgerPath = Path.Combine(_currentWorkspacePath, ".nexus", "memory", "error_ledger.json");
                        try
                        {
                            List<string>? errorsList = null;
                            if (File.Exists(ledgerPath))
                            {
                                try
                                {
                                    string existingJson = await File.ReadAllTextAsync(ledgerPath);
                                    errorsList = JsonSerializer.Deserialize<List<string>>(existingJson);
                                }
                                catch (JsonException)
                                {
                                    // [SELF-HEALING]: Se il file è corrotto, resettalo in sicurezza per sbloccare la scrittura!
                                    errorsList = new List<string> { "[SISTEMA]: Registro degli errori resettato per corruzione." };
                                }
                            }

                            if (errorsList == null)
                            {
                                errorsList = new List<string> { "[SISTEMA]: Registro degli errori passati." };
                            }

                            string safeError = cleanErrorLog.Replace("\"", "'").Replace("\n", " | ").Replace("\r", "");
                            if (safeError.Length > 500) safeError = safeError.Substring(0, 500) + "...";

                            errorsList.Add($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - FATAL ERROR: {safeError}");

                            string updatedJson = JsonSerializer.Serialize(errorsList, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(ledgerPath, updatedJson, Encoding.UTF8);
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE AGGIORNAMENTO MEMORIA]: {ex.Message}", Brushes.Red));
                        }
                    }
                    // --- FINE FIX FASE 4 ---

                    Dispatcher.UIThread.Post(() => AppendToChat($"[GIUDICE PRATICO]: ❌ Compilazione fallita. Innesco Auto-Riparazione...\n{cleanErrorLog}", Avalonia.Media.Brushes.Tomato));

                    // --- INIZIO PATCH INTELLIGENZA: LETTURA FILE SORGENTE ---
                    HashSet<string> filesToRead = new HashSet<string>();
                    StringBuilder sourceCodeContext = new StringBuilder();

                    // Estraiamo i percorsi dei file direttamente dalle righe di errore del compilatore
                    foreach (var line in errorLines)
                    {
                        int parenthesisIndex = line.IndexOf('(');
                        if (parenthesisIndex > 0)
                        {
                            string potentialFile = line.Substring(0, parenthesisIndex).Trim();
                            if (File.Exists(potentialFile))
                            {
                                filesToRead.Add(potentialFile);
                            }
                        }
                    }

                    // Se non riusciamo a mappare il file dall'errore, peschiamo almeno i file vitali che sappiamo essere spesso coinvolti
                    if (filesToRead.Count == 0)
                    {
                        string mainWindowCs = Path.Combine(_currentWorkspacePath, "Views", "MainWindow.axaml.cs");
                        string mainWindowAxaml = Path.Combine(_currentWorkspacePath, "Views", "MainWindow.axaml");
                        if (File.Exists(mainWindowCs)) filesToRead.Add(mainWindowCs);
                        if (File.Exists(mainWindowAxaml)) filesToRead.Add(mainWindowAxaml);
                    }

                    // Leggiamo i file trovati e prepariamo il blocco di contesto
                    foreach (var file in filesToRead)
                    {
                        try
                        {
                            string content = await File.ReadAllTextAsync(file);
                            // Estraiamo il path relativo (es. Views/MainWindow.axaml.cs)
                            string relativePath = file.Replace(_currentWorkspacePath + Path.DirectorySeparatorChar.ToString(), "");
                            
                            sourceCodeContext.AppendLine($"=== SORGENTE ATTUALE CON ERRORI: [{relativePath}] ===");
                            sourceCodeContext.AppendLine("```csharp\n" + content + "\n```\n");
                        }
                        catch { }
                    }
                    // --- FINE PATCH INTELLIGENZA ---

                    // >>> SPOSTAMENTO DELLA MEMORIA ESCALATION QUI <<<
                    // Salviamo i dati finali PRIMA di lanciare il Coder, così siamo certi che il sourceCodeContext sia pieno
                    lastFatalError = cleanErrorLog;
                    lastFatalSourceContext = sourceCodeContext.ToString();

                    string fixPrompt = $@"Il compilatore C# ha restituito questi ESATTI ERRORI BLOCCANTI durante la compilazione:
{cleanErrorLog}

Ecco il CODICE SORGENTE ATTUALE dei file che generano l'errore:
{sourceCodeContext.ToString()}

Analizza gli errori e applica una CORREZIONE CHIRURGICA ai file sorgente.
REGOLA FONDAMENTALE: NON STRAVOLGERE IL CODICE ESISTENTE. Mantieni l'architettura MVC, mantieni intatti i metodi asincroni e la logica che già funciona. REGOLA FONDAMENTALE: Mantieni intatta l'architettura generale MVC, ma sei libero di riscrivere la logica interna dei metodi, aggiungere controlli (if/else), usare variabili temporanee (come Path.Combine) e modificare la sintassi se questo è necessario per risolvere l'errore di compilazione alla radice.

Non aggiungere spiegazioni testuali.
Per OGNI file che modifichi, DEVI TASSATIVAMENTE usare questo esatto formato, apici inclusi:
[FILE: Cartella/NomeDelFile.estensione]
```csharp
// intero codice corretto
```";

                    // Chiamata diretta al Coder per riparare il codice con memoria piena
                    await DelegateFixToCoderAsync(fixPrompt);
                    attempt++;
                }
            }

            if (!compilationSuccess)
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 🚨 Auto-Guarigione (Livello 1) fallita. Innesco ESCALATION al Master Mentor (Livello 2)...", Avalonia.Media.Brushes.OrangeRed));

                // Prepariamo il Dossier per l'Architetto (VERSIONE BLINDATA E UNIVERSALE)
                string escalationPrompt = $@"[SISTEMA: ESCALATION CRITICA COMPILATORE]
Il Coder è bloccato in un loop di errori C# (tentativi falliti: {maxRetries}).

LOG ERRORE FATALE:
{lastFatalError}

SORGENTE INCRIMINATO:
{lastFatalSourceContext}

DIRETTIVA TASSATIVA PER IL MENTORE:
1. Analizza rigorosamente il LOG ERRORE FATALE e individua la causa esatta (es. sintassi obsoleta di Avalonia 11, errori di escape, riferimenti mancanti).
2. Spiega brevemente l'approccio sicuro per risolverlo.
3. Concentrati ESCLUSIVAMENTE sui file menzionati in QUESTO errore. Dimentica i bug affrontati nelle iterazioni precedenti (ignora vecchie classi se non sono menzionate qui).
4. DEVI OBBLIGATORIAMENTE chiudere la tua analisi con un blocco JSON e il tag [GENERA_CODICE], come imposto dalle deine regole di sistema.
Questo è necessario per innescare la catena di automazione.";

                // Dirottiamo la richiesta al Master Mentor come se la stessi scrivendo tu!
                await InvokeArchitectAsync(escalationPrompt, 0, false);
            }
        }

	// ==========================================================
        // MOTORE SUPERVISORE DI ESECUZIONE (CATTURA CRASH A RUNTIME)
        // ==========================================================
        private async Task ExecuteAndSuperviseAppAsync()
        {
            if (string.IsNullOrEmpty(_currentWorkspacePath)) return;

            Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: 🚀 Esecuzione in corso. Il Supervisore è in ascolto per eventuali crash...", Avalonia.Media.Brushes.Gold));

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run",
                WorkingDirectory = _currentWorkspacePath,
                RedirectStandardError = true, // INTERCETTIAMO IL FLUSSO DI ERRORE
                UseShellExecute = false,      // Necessario per leggere lo stream
                CreateNoWindow = true
            };

            try
            {
                using var process = new System.Diagnostics.Process { StartInfo = psi };
                
                System.Text.StringBuilder errorLog = new System.Text.StringBuilder();
                process.ErrorDataReceived += (sender, args) => {
                    if (!string.IsNullOrEmpty(args.Data)) errorLog.AppendLine(args.Data);
                };

                process.Start();
                process.BeginErrorReadLine(); // Lettura asincrona per evitare blocchi
                
                await process.WaitForExitAsync();

                int exitCode = process.ExitCode;
                string errors = errorLog.ToString();

                // SEGNALE DI SCHIANTO A RUNTIME: Exit code non 0 e presenza di un'eccezione
                if (exitCode != 0 && (errors.Contains("Unhandled exception") || errors.Contains("Exception")))
                {
                    Dispatcher.UIThread.Post(() => 
                    {
                        var (_, container) = AppendToChat($"[SUPERVISORE]: ⚠️ L'app è crashata a runtime!\n{errors.Trim()}", Avalonia.Media.Brushes.Tomato);
                        
                        // IL CONTROLLO UMANO: Un bottone per decidere se innescare il Coder
                        var fixBtn = new Avalonia.Controls.Button 
                        { 
                            Content = "🔧 Innesca Auto-Riparazione del Crash", 
                            Background = Avalonia.Media.Brushes.OrangeRed, 
                            Foreground = Avalonia.Media.Brushes.White,
                            Margin = new Avalonia.Thickness(0, 10, 0, 0),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                        };
                        
                        fixBtn.Click += async (s, e) => 
                        {
                            fixBtn.IsEnabled = false;
                            fixBtn.Content = "⏳ Analisi Coder in corso...";

                            // --- PATCH VISTA: Iniettiamo il codice sorgente reale ---
                            string mwCsPath = System.IO.Path.Combine(_currentWorkspacePath, "Views", "MainWindow.axaml.cs");
                            string fileContent = System.IO.File.Exists(mwCsPath) ? System.IO.File.ReadAllText(mwCsPath) : "// File non trovato";
                            
                            // Variabile per non far impazzire il parser Markdown della chat
                            string backticks = "\u0060\u0060\u0060";
                            
                            // Prepariamo il prompt chirurgico per il Coder INIETTANDO IL CODICE REALE
                            string fixPrompt = $@"L'applicazione compila con successo ma va in crash a runtime con questa eccezione:
{errors}

Questo è l'ESATTO CODICE ATTUALE del file in cui avviene il crash:
{backticks}csharp
{fileContent}
{backticks}

Analizza rigorosamente l'errore e rigenera i file interessati risolvendo il problema. 
REGOLA 1: MANTIENI INTATTA LA LOGICA ASINCRONA E L'ARCHITETTURA ESISTENTE.
REGOLA 2: Usa TASSATIVAMENTE il tag [FILE: path] seguito dai tre apici per permettere il salvataggio.
REGOLA 3 (ANTI-PIGRIZIA): DEVI SEMPRE RISCRIVERE IL FILE PER INTERO, dalla prima all'ultima riga. È assolutamente vietato troncare il codice e non devi MAI omettere l'attributo 'x:Class' nei file axaml.";
                            
                            // Ricicliamo il tuo fantastico metodo di auto-fix per lanciare la correzione!
                            await DelegateFixToCoderAsync(fixPrompt);

                            // --- IL POSTO CORRETTO PER IL RIAVVIO ---
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendToChat("\n[SISTEMA]: 🔄 Patch a runtime applicata. Riavvio del ciclo di compilazione per verifica...", Avalonia.Media.Brushes.Gold));
                            await RunAutoCompilationLoopAsync();
                            // ----------------------------------------
                        };
                        
                        container.Children.Add(fixBtn);
                    });
                }
                else if (exitCode != 0)
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: L'app è stata chiusa con codice di uscita anomalo ({exitCode}), ma senza eccezioni gestite.", Avalonia.Media.Brushes.Gray));
                }
                else
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: L'app si è chiusa in modo pulito.", Avalonia.Media.Brushes.SpringGreen));
                }
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE SUPERVISORE]: {ex.Message}", Avalonia.Media.Brushes.Red));
            }
        }

	private async Task DelegateFixToCoderAsync(string fixPrompt)
        {
            // --- FIX FASE 4: CARICHIAMO IL LEDGER ANCHE NELL'AUTO-FIXER ---
            string coderLTM = "";
            if (!string.IsNullOrEmpty(_currentWorkspacePath))
            {
                string ledgerPath = Path.Combine(_currentWorkspacePath, ".nexus", "memory", "error_ledger.json");
                if (File.Exists(ledgerPath))
                {
                    try
                    {
                        string jsonContent = await File.ReadAllTextAsync(ledgerPath);
                        List<string>? errors = JsonSerializer.Deserialize<List<string>>(jsonContent);
                        if (errors != null && errors.Count > 1)
                        {
                            // Esclude la nota iniziale del sistema per dare solo i veri log di errore
                            coderLTM = "\n\n=== MEMORIA RECENTE DEGLI ERRORI COMPILATORE (DA NON RIPETERE MAI PIÙ) ===\n" +
                                       string.Join("\n", errors.Skip(1).TakeLast(5)) + // Ottimizza la finestra di contesto agli ultimi 5 errori
                                       "\n=======================================================================\n";
                        }
                    }
                    catch
                    {
                        // Fallback e auto-pulizia logica se corrotto
                        coderLTM = "\n\n=== REGISTRO ERRORI (MALFORMATO O VUOTO - RESET COMPILATORE RICHIESTO) ===\n";
                    }
                }
            }

            // --- LEGGE IL BLUEPRINT DEL PROGETTO DA PASSARE AL CODER PER L'AUTO-FIX --- [PATCH CONTESTO APPLICATA]
            string blueprintContext = "";
            if (!string.IsNullOrEmpty(_currentWorkspacePath))
            {
                string blueprintPath = Path.Combine(_currentWorkspacePath, ".nexus", "blueprint.md");
                if (File.Exists(blueprintPath))
                {
                    try
                    {
                        string bpContent = await File.ReadAllTextAsync(blueprintPath);
                        blueprintContext = $"\n=== BLUEPRINT DEL PROGETTO (OBIETTIVO MACRO) ===\n{bpContent}\n================================================\n";
                    }
                    catch { }
                }
            }

            // --- RECUPERA L'ULTIMA DIRETTIVA DELL'ARCHITETTO/MENTOR DALLA CHAT --- [PATCH CONTESTO APPLICATA]
            string architectContext = "";
            if (_chatHistory != null && _chatHistory.Count > 0)
            {
                var lastAssistant = _chatHistory.LastOrDefault(m => m["role"] == "assistant");
                if (lastAssistant != null)
                {
                    architectContext = $"\n=== DIRETTIVA INIZIALE DELL'ARCHITETTO (COSA STIAMO CERCANDO DI COSTRUIRE) ===\n{lastAssistant["content"]}\n=========================================================================\n";
                }
            }

            string systemInstruction = $@"Sei il Coder (Ingegnere Riparatore) di Opera Suprema. Il tuo unico scopo è analizzare gli errori e riparare chirurgicamente il codice sorgente.
REGOLA 1 (AVALONIA 11 E UI): Assicurati sempre che il costruttore della finestra chiami InitializeComponent(); prima di recuperare i controlli con this.FindControl<T>(""NomeControllo"").
REGOLA 2 (PRESERVAZIONE LOGICA): NON aggiungere pattern architetturali (es. MVVM) non richiesti. Mantieni intatti i metodi originali.
REGOLA 3 (ANTI-PIGRIZIA E ANTI-TRONCAMENTO): È ASSOLUTAMENTE VIETATO troncare il codice o usare commenti come '// resto del codice'. DEVI SEMPRE RESTITUIRE OGNI FILE PER INTERO, dalla prima all'ultima riga. Non omettere MAI attributi vitali come x:Class nei file .axaml.
REGOLA SUPREMA DI FORMATTAZIONE: Per OGNI file, usa TASSATIVAMENTE questo formato:
[FILE: Cartella/NomeDelFile.estensione]
```csharp
// codice corretto
{coderLTM}";

	    // --- ASSEMBLAGGIO INTELLIGENTE DEL PAYLOAD DI CONTESTO --- [PATCH CONTESTO APPLICATA]

            var messagesList = new List<object>();
            messagesList.Add(new { role = "system", content = systemInstruction });
            
            if (!string.IsNullOrEmpty(blueprintContext))
            {
                messagesList.Add(new { role = "system", content = blueprintContext });
            }
            
            if (!string.IsNullOrEmpty(architectContext))
            {
                messagesList.Add(new { role = "system", content = architectContext });
            }
            
            messagesList.Add(new { role = "user", content = fixPrompt });

            var payload = new {
                messages = messagesList,
                temperature = 0.1,       // Quasi zero creatività
                max_tokens = 8192,       // Spazio ampio per scrivere codice lungo
                stream = true,
                frequency_penalty = 0.0, // PENALITÀ ESTREMA: vieta matematicamente di ripetere le stesse parole
                presence_penalty = 0.0   // Obbliga il modello a introdurre nuovi concetti e chiudere il file
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8082/v1/chat/completions") {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            };

            try
            {
                var (aiMessageBlock, _) = AppendToChat("[CODER (Auto-Fix)]:\n", Avalonia.Media.Brushes.Cyan, true, "[CODER (Auto-Fix)]:\n");
                var chatPanel = this.FindControl<Avalonia.Controls.StackPanel>("ChatLogPanel");
                var scrollViewer = chatPanel?.Parent as Avalonia.Controls.ScrollViewer;

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new System.IO.StreamReader(stream);
                System.Text.StringBuilder coderFullResponse = new System.Text.StringBuilder();

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (line.StartsWith("data: ") && line.Substring(6) != "[DONE]")
                    {
                        var doc = System.Text.Json.JsonDocument.Parse(line.Substring(6));
                        var delta = doc.RootElement.GetProperty("choices")[0].GetProperty("delta");
                        if (delta.TryGetProperty("content", out var contentElement))
                        {
                            string chunk = contentElement.GetString() ?? "";
                            coderFullResponse.Append(chunk);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => { aiMessageBlock.Text += chunk; scrollViewer?.ScrollToEnd(); });

                            // =========================================================
                            // KILL-SWITCH LATO C#: ABORTO ISTANTANEO DEL LOOP INFINITO
                            // =========================================================
                            string currentOutput = coderFullResponse.ToString();
                            if (currentOutput.Split("private Button").Length > 8 || currentOutput.Split("private CheckBox").Length > 8)
                            {
                                Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendToChat("\n[SISTEMA]: 🛑 KILL-SWITCH ATTIVATO! Rilevato loop di allucinazione. Connessione troncata per salvare la VRAM.", Avalonia.Media.Brushes.Red));
                                break; 
                            }
                        }
                    }
                }

                string generatedCode = coderFullResponse.ToString();
                
                // Innesco automatico del salvataggio
                AutonomousProjectGenerator(generatedCode);
            }
            catch (Exception ex) 
            { 
                Avalonia.Threading.Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE AUTO-FIX]: {ex.Message}", Avalonia.Media.Brushes.Red)); 
            }
        }

	// --- MOTORE DI RICERCA FUZZY PER FILE DUPLICATI ---
        private string? FuzzyFindExistingFile(string aiRawPath, string workspacePath)
        {
            string rawFileName = System.IO.Path.GetFileName(aiRawPath).ToLower();
            
            // Determiniamo la "natura" del file (è un file C# code-behind/classe o è UI pura?)
            bool isCodeFile = rawFileName.EndsWith(".cs");

            // Rimuoviamo qualsiasi estensione fantasiosa per ottenere la "radice" (es. "mainwindow")
            string[] extensionsToStrip = { ".axaml.cs", ".xaml.cs", ".axml.cs", ".axaml", ".xaml", ".axml", ".xml", ".cs" };
            string baseName = rawFileName;
            foreach (var ext in extensionsToStrip)
            {
                if (baseName.EndsWith(ext))
                {
                    baseName = baseName.Substring(0, baseName.Length - ext.Length);
                    break;
                }
            }

            try
            {
                var allFiles = System.IO.Directory.GetFiles(workspacePath, "*.*", System.IO.SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"));

                foreach (var file in allFiles)
                {
                    string existingFileName = System.IO.Path.GetFileName(file).ToLower();
                    bool existingIsCode = existingFileName.EndsWith(".cs");

                    // Se uno è codice puro e l'altro è UI, non sono la stessa cosa, saltiamo
                    if (isCodeFile != existingIsCode) continue;

                    // Estraiamo la radice del file vero sul disco
                    string existingBaseName = existingFileName;
                    foreach (var ext in extensionsToStrip)
                    {
                        if (existingBaseName.EndsWith(ext))
                        {
                            existingBaseName = existingBaseName.Substring(0, existingBaseName.Length - ext.Length);
                            break;
                        }
                    }

                    // Se le due radici combaciano perfettamente, è il nostro file!
                    if (existingBaseName == baseName)
                    {
                        return file; 
                    }
                }
            }
            catch { }

            return null;
        }

	// ==========================================================
        // RADAR MEMORIA UNIFICATA (CROSS-PLATFORM: LINUX & WINDOWS)
        // ==========================================================
        private async Task<int> GetAvailableRamMbAsync()
        {
            try
            {
                // --- LOGICA PER LINUX ---
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    var lines = await System.IO.File.ReadAllLinesAsync("/proc/meminfo");
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemAvailable:"))
                        {
                            var parts = line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && long.TryParse(parts[1], out long kb))
                            {
                                return (int)(kb / 1024);
                            }
                        }
                    }
                }
                // --- LOGICA PER WINDOWS ---
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    // Usa uno script WMI per leggere la memoria disponibile (veloce e nativo)
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "wmic",
                        Arguments = "OS get FreePhysicalMemory /Value",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var process = System.Diagnostics.Process.Start(psi);
                    if (process != null)
                    {
                        string output = await process.StandardOutput.ReadToEndAsync();
                        await process.WaitForExitAsync();

                        var lines = output.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines)
                        {
                            if (line.Contains("FreePhysicalMemory="))
                            {
                                string valueStr = line.Split('=')[1].Trim();
                                if (long.TryParse(valueStr, out long kb))
                                {
                                    return (int)(kb / 1024);
                                }
                            }
                        }
                    }
                }
            }
            catch 
            {
                // Fallback silenzioso
            }
            
            // Se tutto fallisce, restituiamo un valore alto per non bloccare l'avvio forzato dei modelli
            return 999999; 
        }

	// ==========================================================
        // ORCHESTRATORE SMART SWAP (PING-PONG INTELLIGENTE)
        // ==========================================================
        private async Task SmartModelSwapAsync(string modelToKill, string modelToStart, string statusMessage)
        {
            // 1. Accendiamo l'Overlay per l'operatore
            Dispatcher.UIThread.Post(() => {
                var overlay = this.FindControl<Avalonia.Controls.Border>("SwapOverlay");
                var statusText = this.FindControl<Avalonia.Controls.TextBlock>("SwapStatusText");
                if (statusText != null) statusText.Text = statusMessage;
                if (overlay != null) overlay.IsVisible = true;
            });

            // Simuliamo il toggle "Hot-Swap" delle impostazioni (presto lo collegheremo al ConfigManager)
            bool hotSwapEnabled = _configManager.CurrentConfig.HotSwapEnabled; 

            // 2. Protezione Anti Out-Of-Memory
            if (!hotSwapEnabled)
            {
                int availableRam = await GetAvailableRamMbAsync();
                // Soglia di emergenza reale: interveniamo solo se scendiamo sotto i 4GB liberi
                if (availableRam < 4000) 
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: ⚠️ RAM unificata in esaurimento ({availableRam}MB liberi). Attivazione forzata dello Swap per evitare il blocco del sistema.", Avalonia.Media.Brushes.OrangeRed));
                    hotSwapEnabled = true; // Forza l'uccisione del modello precedente
                }
                else
                {
                    Dispatcher.UIThread.Post(() => AppendToChat($"[SISTEMA]: ✅ RAM sufficiente rilevata ({availableRam}MB liberi). Modelli affiancati in VRAM.", Avalonia.Media.Brushes.SpringGreen));
                }
            }

            // 3. Esecuzione del Kill selettivo (se necessario)
            if (hotSwapEnabled && !string.IsNullOrEmpty(modelToKill))
            {
                _containerManager.KillContainer(modelToKill);
                await Task.Delay(2000); // Attesa fisica per lo svuotamento dei buffer della VRAM
            }

            // 4. Montaggio del nuovo Modello
            var modelConfig = _configManager.CurrentConfig.Modes["HACKER"].FirstOrDefault(m => m.Id == modelToStart);
            if (modelConfig != null)
            {
                string storagePath = _configManager.CurrentConfig.StoragePath;
                await _containerManager.StartContainerAsync(modelConfig.Id, $"{storagePath}/{modelConfig.FileName}", modelConfig.Port, modelConfig.ContextSize);
            }

            // 5. Spegnimento dell'Overlay
            Dispatcher.UIThread.Post(() => {
                var overlay = this.FindControl<Avalonia.Controls.Border>("SwapOverlay");
                if (overlay != null) overlay.IsVisible = false;
            });
        }

	// ==========================================================
        // GESTORE BLUEPRINT (CREAZIONE E APERTURA NATIVA SU LINUX)
        // ==========================================================
        private async Task OpenOrInitializeBlueprintAsync()
        {
            if (string.IsNullOrEmpty(_currentWorkspacePath))
            {
                Dispatcher.UIThread.Post(() => AppendToChat("[SISTEMA]: ⚠️ Nessun progetto caricato. Collega prima una cartella dal Nexus Explorer.", Avalonia.Media.Brushes.Orange));
                return;
            }

            string nexusDir = Path.Combine(_currentWorkspacePath, ".nexus");
            string blueprintPath = Path.Combine(nexusDir, "blueprint.md");

            try
            {
                if (!Directory.Exists(nexusDir)) Directory.CreateDirectory(nexusDir);

                if (!File.Exists(blueprintPath))
                {
                    string template = @"# 📜 Blueprint del Progetto\n\n**Obiettivo Principale:**\n[Inserisci qui l'obiettivo]";
                    await File.WriteAllTextAsync(blueprintPath, template);
                }

                // APRE LA NOSTRA NUOVA FINESTRA MODALE GUIDATA CON I TOOLTIP ?
                var editorWindow = new BlueprintEditorWindow(blueprintPath);
                await editorWindow.ShowDialog(this);

                Dispatcher.UIThread.Post(() => AppendToChat("[SISTEMA]: 📜 Blueprint aggiornato e sincronizzato con successo.", Avalonia.Media.Brushes.LightGreen));
            }
            catch (Exception ex)
            {
                Dispatcher.UIThread.Post(() => AppendToChat($"[ERRORE BLUEPRINT]: Impossibile aprire l'editor guidato. {ex.Message}", Avalonia.Media.Brushes.Red));
            }
        }
    }
}