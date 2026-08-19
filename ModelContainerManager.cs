using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace OperaSuprema.Core
{
    public class ModelContainerManager
    {
        private readonly ConcurrentDictionary<string, Process> _activeContainers = new();
        private readonly ConcurrentDictionary<string, bool> _isRecovering = new(); 
        private readonly ConcurrentDictionary<string, bool> _intentionalKills = new(); 

        private bool _isShuttingDown = false;

        // Modifica la firma per ricevere il path
        public async Task StartContainerAsync(string roleName, string modelPath, int port, int contextSize = 8192, string mmprojPath = "", bool useFlashAttention = false, string kvCacheType = "f16", string llamaExePath = "llama-server")
        {
            if (_activeContainers.ContainsKey(roleName))
            {
                Console.WriteLine($"[INFO] {roleName} già attivo su porta {port}.");
                return;
            }

            try
            {
                Console.WriteLine($"[SISTEMA] Boot {roleName} → Porta {port}...");

                // 1. CREIAMO IL NOSTRO SENSORE DI "PRONTO"
                var tcs = new TaskCompletionSource<bool>();

                // Base universale per tutti i modelli
                string args = $"-m \"{modelPath}\" -c {contextSize} -b 2048 -ub 2048 --port {port}";
                args += " --parallel 2 --n-gpu-layers 999";
        
                if (useFlashAttention) args += " -fa on";
                args += $" -ctk {kvCacheType} -ctv {kvCacheType}"; 

                if (roleName == "EmbeddingEngine") args += " --embeddings";
                
                if (!string.IsNullOrEmpty(mmprojPath))
                {
                    args += $" --mmproj \"{mmprojPath}\"";
                    Console.WriteLine($"[VISION] Proiettore agganciato: {mmprojPath}");
                }

                // Cerca poco più giù dove si inizializza il ProcessStartInfo e sostituiscilo così:
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    // Usiamo il path custom se passato, altrimenti fallback sul comando di sistema 'llama-server'
                    FileName = string.IsNullOrWhiteSpace(llamaExePath) ? "llama-server" : llamaExePath,
                    Arguments = args, // il resto della configurazione rimane identica a come ce l'avevi
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                Process process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                process.ErrorDataReceived += (sender, errArgs) =>
                {
                    if (!string.IsNullOrEmpty(errArgs.Data))
                    {
                        Console.WriteLine($"[{roleName} ERRORE C++]: {errArgs.Data}");

                        // 📡 RADAR DI CARICAMENTO: Appena il server annuncia di essere in ascolto, passiamo il testimone!
                        if (errArgs.Data.Contains("llama_server: listening on http"))
                        {
                            tcs.TrySetResult(true);
                        }
                    }
                };

                process.Exited += (sender, args) =>
                {
                    // 🚨 Se crasha prima di caricare, sblocca subito l'attesa per non lasciare l'IDE appeso!
                    tcs.TrySetResult(false); 

                    if (_isShuttingDown) return;
                    
                    _activeContainers.TryRemove(roleName, out _);

                    if (_intentionalKills.TryRemove(roleName, out _)) return; 

                    Console.WriteLine($"[CRITICAL] {roleName} (Porta {port}) crashato!");
                    
                    if (_isRecovering.ContainsKey(roleName)) return;
                    _isRecovering.TryAdd(roleName, true);
                    
                    Task.Run(async () =>
                    {
                        await Task.Delay(5000); 
                        Console.WriteLine($"[RECOVERY] Ripristino {roleName}...");
                        try
                        {
                            await StartContainerAsync(roleName, modelPath, port, contextSize, mmprojPath, useFlashAttention, kvCacheType, llamaExePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RECOVERY FALLITO] {ex.Message}");
                        }
                        finally
                        {
                            _isRecovering.TryRemove(roleName, out _);
                        }
                    });
                };

                process.Start();
                process.BeginErrorReadLine();

                _activeContainers.TryAdd(roleName, process);
                Console.WriteLine($"[OK] {roleName} avviato (PID: {process.Id}). In attesa del caricamento pesi in VRAM...");
                
                // ⏱️ GESTIONE DEL TIMEOUT INTELLIGENTE (Es. Massimo 60 secondi per non bloccare l'IDE all'infinito)
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(60));
                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine($"[WARNING] {roleName} ha superato il tempo limite di caricamento (60s). L'API potrebbe non rispondere.");
                }
                else
                {
                    bool isLoaded = await tcs.Task;
                    if (isLoaded)
                    {
                        Console.WriteLine($"[SISTEMA] ✅ {roleName} pienamente operativo. Testimone passato!");
                    }
                    else
                    {
                        Console.WriteLine($"[SISTEMA] ❌ Impossibile passare il testimone: {roleName} è andato in crash durante l'avvio.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERRORE FATALE] {roleName}: {ex.Message}");
            }
        }

        public void KillAllContainers()
        {
            _isShuttingDown = true; 
            
            foreach (var kvp in _activeContainers)
            {
                try
                {
                    if (!kvp.Value.HasExited)
                    {
                        kvp.Value.Kill();
                        Console.WriteLine($"[STOP] {kvp.Key} terminato.");
                    }
                }
                catch { }
            }
            _activeContainers.Clear();
            _isRecovering.Clear();
            _intentionalKills.Clear();
            Console.WriteLine("[SISTEMA] Tutti i container neurali offline.");
        }

        public void KillContainer(string roleName)
        {
            if (_activeContainers.TryGetValue(roleName, out Process? process))
            {
                try
                {
                    if (!process.HasExited)
                    {
                        _intentionalKills.TryAdd(roleName, true); 
                        process.Kill();
                        Console.WriteLine($"[STOP] {roleName} smontato a caldo dalla VRAM.");
                    }
                }
                catch { }
                _isRecovering.TryRemove(roleName, out _); 
            }
        }
    }
}