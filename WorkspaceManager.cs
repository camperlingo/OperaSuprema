using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OperaSuprema.Core
{
    public class WorkspaceManager
    {
        public string CurrentProjectPath { get; private set; }
        private readonly List<string> _supportedExtensions = new() { ".cs", ".axaml", ".json", ".py", ".sh" };

        public WorkspaceManager(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException("Percorso progetto non valido.");
                
            CurrentProjectPath = folderPath;
        }

        /// <summary>
        /// Genera il super-prompt architetturale che mappa l'intero progetto mattone dopo mattone.
        /// </summary>
        public string GenerateProjectContextPayload()
        {
            var payloadBuilder = new StringBuilder();
            payloadBuilder.AppendLine("=== SYSTEM CONTEXT: LIVE PROJECT STRUCTURE ===");
            payloadBuilder.AppendLine($"Root Path: {CurrentProjectPath}");
            payloadBuilder.AppendLine("Di seguito è riportato l'intero stato attuale del codice sorgente del progetto. Mantieni la coerenza assoluta con questa architettura.");
            payloadBuilder.AppendLine("==============================================");

            // Scansione ricorsiva della directory filtrando i file inutili (es. obj, bin, .git)
            var allFiles = Directory.GetFiles(CurrentProjectPath, "*.*", SearchOption.AllDirectories);

            foreach (var file in allFiles)
            {
                string extension = Path.GetExtension(file);
                // Evitiamo di intasare la memoria con binari o file di build
                if (!_supportedExtensions.Contains(extension) || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(CurrentProjectPath, file);
                string fileContent = File.ReadAllText(file);

                payloadBuilder.AppendLine($"\n--- FILE START: {relativePath} ---");
                payloadBuilder.AppendLine(fileContent);
                payloadBuilder.AppendLine($"--- FILE END: {relativePath} ---");
            }

            payloadBuilder.AppendLine("\n==============================================");
            payloadBuilder.AppendLine("FINE COGNITIVA DEL PROGETTO. Ora rispondi alla richiesta dell'utente tenendo conto di questa infrastruttura.");
            
            return payloadBuilder.ToString();
        }
    }
}