using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace OperaSuprema.Core
{
    public class ReactiveWorkspace
    {
        private FileSystemWatcher? _watcher;
        private WorkspaceManager? _workspaceManager;
        
        // Aggiungiamo il riferimento al database vettoriale
        private dynamic? _vectorMemory; 

        // Modifica: Aggiunto parametro per ricevere l'istanza della memoria vettoriale
        public void StartMonitoring(string projectPath, WorkspaceManager manager, dynamic vectorMemory)
        {
            _workspaceManager = manager;
            _vectorMemory = vectorMemory;

            _watcher = new FileSystemWatcher(projectPath);
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName;
            
            _watcher.Filter = "*.*"; 
            _watcher.IncludeSubdirectories = true;

            _watcher.Changed += OnWorkspaceChanged;
            _watcher.Created += OnWorkspaceChanged;
            _watcher.Deleted += OnWorkspaceChanged;
            _watcher.Renamed += OnWorkspaceRenamed;

            _watcher.EnableRaisingEvents = true;
        }

        private void OnWorkspaceChanged(object sender, FileSystemEventArgs e)
        {
            // Evitiamo le cartelle temporanee, bin, obj, git
            if (e.FullPath.Contains("/bin/") || e.FullPath.Contains("/obj/") || e.FullPath.Contains(".git")) return;

            var currentMemory = _vectorMemory;
            if (currentMemory != null)
            {
                Task.Run(async () => 
                {
                    string fileContent = null!;
                    int maxRetries = 5;
                    int delayMs = 300;

                    // --- PATCH: Retry Pattern per eludere il File Locking di dotnet o del Coder ---
                    for (int i = 0; i < maxRetries; i++)
                    {
                        try 
                        {
                            fileContent = await File.ReadAllTextAsync(e.FullPath);
                            break; // Lettura riuscita, sblocco l'handle e interrompo il ciclo
                        }
                        catch (IOException)
                        {
                            if (i == maxRetries - 1) return; // Fallimento accettato dopo 1.5 sec
                            await Task.Delay(delayMs);
                            delayMs *= 2; // Backoff esponenziale
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERRORE RAG SYSTEM]: {ex.Message}");
                            return;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(fileContent))
                    {
                        string nomeFile = Path.GetFileName(e.FullPath);
                        int chunkSize = 1500;
                        for (int i = 0; i < fileContent.Length; i += chunkSize)
                        {
                            string chunk = fileContent.Substring(i, Math.Min(chunkSize, fileContent.Length - i));
                            // Usa il nome del file reale nel tag, così il DB capisce cosa sta aggiornando
                            await currentMemory.MemorizeContentAsync($"[LIVE_SYNC: {nomeFile}]", chunk);
                        }
                    }
                });
            }
        }

        private void OnWorkspaceRenamed(object sender, RenamedEventArgs e) => OnWorkspaceChanged(sender, e);
    }
}