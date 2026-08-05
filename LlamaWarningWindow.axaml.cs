using Avalonia.Controls;
using Avalonia.Interactivity;

namespace OperaSuprema.GUI
{
    public partial class LlamaWarningWindow : Window
    {
        private bool _isItalian = true;

        private readonly string _titleIt = "⚠️ Eseguibile llama-server non trovato!";
        private readonly string _titleEn = "⚠️ llama-server executable not found!";

        private readonly string _msgIt = 
            "Opera Suprema è il 'cervello' logico e l'interfaccia utente, ma per far funzionare i modelli AI richiede il motore open-source llama.cpp.\n\n" +
            "Per favore, scarica l'eseguibile corretto per il tuo hardware dal repository ufficiale:\n" +
            "• Nvidia GPU: Scarica la versione CUDA.\n" +
            "• AMD GPU: Scarica la versione ROCm.\n" +
            "• Mac (Apple Silicon): Scarica la versione Metal / ARM64.\n" +
            "• Solo CPU (Intel/AMD): Scarica la versione standard (AVX2).\n\n" +
            "Una volta scaricato il file, apri le Impostazioni (l'icona a forma di ingranaggio) in Opera Suprema e seleziona il percorso in cui lo hai salvato.";

        private readonly string _msgEn = 
            "Opera Suprema is the logical 'brain' and UI, but it requires the open-source llama.cpp engine to run AI models.\n\n" +
            "Please download the correct executable for your hardware from the official repository:\n" +
            "• Nvidia GPU: Download the CUDA version.\n" +
            "• AMD GPU: Download the ROCm version.\n" +
            "• Mac (Apple Silicon): Download the Metal / ARM64 version.\n" +
            "• CPU only (Intel/AMD): Download the standard (AVX2) version.\n\n" +
            "Once downloaded, open the Settings (gear icon) in Opera Suprema and select the file path where you saved it.";

        public LlamaWarningWindow()
        {
            InitializeComponent();
            
            // Inizializza i testi al primo avvio
            UpdateLanguage();

            // L'operatore '!' tranquillizza il compilatore sull'esistenza del bottone
            this.FindControl<Button>("BtnToggleLang")!.Click += (s, e) => 
            {
                _isItalian = !_isItalian;
                UpdateLanguage();
            };

            // L'operatore '!' tranquillizza il compilatore sull'esistenza del bottone
            this.FindControl<Button>("BtnClose")!.Click += (s, e) => Close();
        }

        private void UpdateLanguage()
        {
            // Applichiamo l'operatore '!' anche ai campi di testo
            this.FindControl<TextBlock>("TxtTitle")!.Text = _isItalian ? _titleIt : _titleEn;
            this.FindControl<TextBlock>("TxtMessage")!.Text = _isItalian ? _msgIt : _msgEn;
            this.FindControl<Button>("BtnToggleLang")!.Content = _isItalian ? "🇬🇧 Switch to English" : "🇮🇹 Passa all'Italiano";
        }
    }
}