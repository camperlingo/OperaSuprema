using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OperaSuprema.GUI
{
    public partial class BlueprintEditorWindow : Window
    {
        private readonly string _blueprintPath = null!;

        public BlueprintEditorWindow()
        {
            InitializeComponent();
        }

        public BlueprintEditorWindow(string blueprintPath) : this()
        {
            _blueprintPath = blueprintPath;
            LoadBlueprintContent();
        }

        private async void LoadBlueprintContent()
        {
            if (File.Exists(_blueprintPath))
            {
                try
                {
                    string content = await File.ReadAllTextAsync(_blueprintPath);
                    var txtObj = this.FindControl<TextBox>("TxtObjective");
                    if (txtObj != null) txtObj.Text = content;
                }
                catch { }
            }
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

        private async void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            var txtObj = this.FindControl<TextBox>("TxtObjective")?.Text ?? "";
            var txtTech = this.FindControl<TextBox>("TxtTechSpecs")?.Text ?? "";
            var txtRules = this.FindControl<TextBox>("TxtRules")?.Text ?? "";

            string formattedMarkdown = $@"# 📜 Blueprint del Progetto

**Obiettivo Principale:**
{txtObj}

**Requisiti Tecnici e Architettura:**
{txtTech}

**Regole di Stile per il Coder:**
{txtRules}
";

            try
            {
                await File.WriteAllTextAsync(_blueprintPath, formattedMarkdown);
            }
            catch { }

            Close();
        }
    }
}