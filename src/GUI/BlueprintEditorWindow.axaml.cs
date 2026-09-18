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
                    var txtBlueprint = this.FindControl<TextBox>("TxtBlueprintContent");
                    if (txtBlueprint != null) txtBlueprint.Text = content;
                }
                catch { }
            }
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();

        private async void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            var txtBlueprint = this.FindControl<TextBox>("TxtBlueprintContent");
            string contentToSave = txtBlueprint?.Text ?? "";

            try
            {
                await File.WriteAllTextAsync(_blueprintPath, contentToSave);
            }
            catch { }

            Close();
        }
    }
}