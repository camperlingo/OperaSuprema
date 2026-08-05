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
            // Evitiamo le cartelle temporanee
            if (e.FullPath.Contains("/bin/") || e.FullPath.Contains("/obj/")) return;

            // Creiamo copie locali (snapshot)
            var currentManager = _workspaceManager;
            var currentMemory = _vectorMemory;

            if (currentManager != null && currentMemory != null)
            {
                // PATCH: Aggiungiamo il punto esclamativo (!) per forzare il compilatore a ignorare il warning
                string nuovoContesto = currentManager!.GenerateProjectContextPayload();
                
                // --- INIEZIONE NEL DATABASE VETTORIALE (PATCH MEMORIA) ---
                Task.Run(async () => 
                {
                    try 
                    {
                        if (!string.IsNullOrWhiteSpace(nuovoContesto))
                        {
                            int chunkSize = 1500;
                            for (int i = 0; i < nuovoContesto.Length; i += chunkSize)
                            {
                                string chunk = nuovoContesto.Substring(i, Math.Min(chunkSize, nuovoContesto.Length - i));
                                await currentMemory!.MemorizeContentAsync("[WORKSPACE_LIVE_CONTEXT]", chunk);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERRORE RAG]: Impossibile sincronizzare la memoria del workspace: {ex.Message}");
                    }
                });

                // Aggiorniamo la UI in sicurezza
                Dispatcher.UIThread.Post(() => {
                    // Qui manderemo i dati visivi alla barra di progresso
                });
            }
        }

        private void OnWorkspaceRenamed(object sender, RenamedEventArgs e) => OnWorkspaceChanged(sender, e);
    }
}