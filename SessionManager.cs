using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace OperaSuprema.Core 
{
    // 1. Definiamo come è fatto un singolo messaggio[cite: 2]
    public class ChatMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    // 2. Definiamo l'intero "Faldone" (La sessione della chat)[cite: 2]
    public class ChatSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Nuova Conversazione";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; } = false;
        public string? ProjectPath { get; set; } // <--- Il magico punto di domanda[cite: 2]
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        
        // NUOVO: Tiene traccia di quanti blocchi abbiamo archiviato su NVMe
        public int StepCount { get; set; } = 0; 
    }

    // 3. Il Motore che gestisce i file fisici[cite: 2]
    public class SessionManager
    {
        private readonly string _genericChatsPath;
        private readonly StepChatManager _stepChatManager; // <-- Il nostro nuovo motore
        
        // Soglia indicativa: 20.000 token (circa 80.000 caratteri) per il taglio automatico
        private const int TokenThreshold = 10000; 

        // Iniezione di dipendenza nel costruttore
        public SessionManager(StepChatManager stepChatManager)
        {
            _stepChatManager = stepChatManager;
            
            // Crea la directory nascosta di default nel sistema Linux dell'utente[cite: 2]
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _genericChatsPath = Path.Combine(homePath, ".config", "OperaSuprema", "Chats");
            Directory.CreateDirectory(_genericChatsPath);
            
            // All'avvio, lancia lo spazzino (ora in modalità asincrona, non lo attendiamo)
            _ = CleanupOldGenericChatsAsync();
        }

        // Funzione per salvare la chat su disco (Ora Asincrona e con Controllo Token)
        public async Task SaveSessionAsync(ChatSession session, string? currentProjectPath = null) 
        {
            session.LastModified = DateTime.Now;
            session.ProjectPath = currentProjectPath;

            // 1. IL TAGLIO AUTOMATICO: Controlliamo se la RAM della chat è satura
            int activeTokens = EstimateTokenCount(session.Messages);
            if (activeTokens > TokenThreshold)
            {
                await ArchiveOldestMessagesAsStepAsync(session);
            }

            // 2. Salvataggio classico del file JSON fisico[cite: 2] (ora snello)
            string saveDir = string.IsNullOrEmpty(currentProjectPath) 
                ? _genericChatsPath 
                : Path.Combine(currentProjectPath, ".nexus", "chats");
            
            Directory.CreateDirectory(saveDir); // Assicura che la cartella esista[cite: 2]
            
            string filePath = Path.Combine(saveDir, $"{session.Id}.json");
            string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            
            await File.WriteAllTextAsync(filePath, json);
        }

        // MOTORE MATEMATICO: Stima veloce dei token (1 token = ~4 caratteri)
        private int EstimateTokenCount(List<ChatMessage> messages)
        {
            int charCount = messages.Sum(m => m.Content?.Length ?? 0);
            return charCount / 4;
        }

        // LOGICA DI CRISTALLIZZAZIONE SULLO STORAGE NVMe
        private async Task ArchiveOldestMessagesAsStepAsync(ChatSession session)
        {
            Console.WriteLine($"[SESSION MANAGER] Soglia token superata per {session.Id}. Avvio archiviazione Step {session.StepCount}...");
            
            // Manteniamo un buffer di sicurezza minimo (almeno 4 messaggi devono sempre restare in RAM)
            if (session.Messages.Count <= 4) return; 

            // Prendiamo la metà più vecchia dei messaggi
            int messagesToArchive = session.Messages.Count / 2;
            
            // --- PATCH JINJA: BILANCIAMENTO DEI RUOLI ---
            // Assicurati di tagliare un numero PARI di messaggi per mantenere l'alternanza user->assistant
            if (messagesToArchive % 2 != 0) messagesToArchive++; 
            if (messagesToArchive >= session.Messages.Count) messagesToArchive = session.Messages.Count - 2;

            if (messagesToArchive <= 0) return;
            // --- FINE PATCH ---

            var oldMessages = session.Messages.Take(messagesToArchive).ToList();
            
            // Uniamo i messaggi per creare il blocco semantico
            string stepContent = string.Join("\n\n", oldMessages.Select(m => $"{m.Role}: {m.Content}"));
            string summary = $"Step archiviato in background il {DateTime.Now:g}";
            
            // Spariamo il blocco su Qdrant
            await _stepChatManager.SaveChatStepAsync(session.Id, session.StepCount, stepContent, summary);
            
            // LA MAGIA: Rimuoviamo i messaggi vecchi dalla RAM e dal JSON, liberando la KV Cache!
            session.Messages.RemoveRange(0, messagesToArchive);
            session.StepCount++;
        }

        // Funzione per caricare TUTTE le chat (Generiche + Quelle del progetto attuale)[cite: 2]
        public List<ChatSession> LoadAllAvailableSessions(string? currentProjectPath = null)
        {
            var sessions = new List<ChatSession>();

            // 1. Carica le chat generiche[cite: 2]
            sessions.AddRange(LoadSessionsFromDirectory(_genericChatsPath));

            // 2. Se c'è un progetto caricato, carica anche i faldoni di quel progetto[cite: 2]
            if (!string.IsNullOrEmpty(currentProjectPath))
            {
                string projectChatsDir = Path.Combine(currentProjectPath, ".nexus", "chats");
                if (Directory.Exists(projectChatsDir))
                {
                    sessions.AddRange(LoadSessionsFromDirectory(projectChatsDir));
                }
            }

            // Ordina: Prima le fissate (Pinned), poi le più recenti[cite: 2]
            return sessions.OrderByDescending(s => s.IsPinned)
                           .ThenByDescending(s => s.LastModified)
                           .ToList();
        }

        private List<ChatSession> LoadSessionsFromDirectory(string directoryPath)
        {
            var list = new List<ChatSession>();
            foreach (var file in Directory.GetFiles(directoryPath, "*.json")) //[cite: 2]
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var session = JsonSerializer.Deserialize<ChatSession>(json);
                    if (session != null) list.Add(session);
                }
                catch { /* Ignora i file corrotti */ } //[cite: 2]
            }
            return list;
        }

        // =================================================================
        // LA NUOVA CANCELLAZIONE DEFINITIVA (CHIRURGIA TOTALE)
        // =================================================================
        public async Task DeleteSessionAsync(ChatSession session)
        {
            // 1. Elimina il file JSON fisico
            string dir = string.IsNullOrEmpty(session.ProjectPath) ? _genericChatsPath : Path.Combine(session.ProjectPath, ".nexus", "chats");
            string file = Path.Combine(dir, $"{session.Id}.json");
            if (File.Exists(file)) File.Delete(file); //[cite: 2]

            // 2. ATTIVA LO SPAZZINO SU NVMe: Cancella la storia della chat da Qdrant
            await _stepChatManager.DeleteChatHistoryAsync(session.Id);
        }

        // Aggiornamento metodi a chiamate Task
        public async Task RenameSessionAsync(ChatSession session, string newTitle)
        {
            session.Title = newTitle; //[cite: 2]
            await SaveSessionAsync(session, session.ProjectPath);
        }

        public async Task TogglePinSessionAsync(ChatSession session)
        {
            session.IsPinned = !session.IsPinned; //[cite: 2]
            await SaveSessionAsync(session, session.ProjectPath);
        }

        // Lo spazzino automatico aggiornato (elimina le chat generiche > 7 giorni se non fissate)[cite: 2]
        private async Task CleanupOldGenericChatsAsync()
        {
            var oldSessions = LoadSessionsFromDirectory(_genericChatsPath)
                .Where(s => !s.IsPinned && (DateTime.Now - s.LastModified).TotalDays > 7);

            foreach (var session in oldSessions)
            {
                await DeleteSessionAsync(session); // Eliminerà sia il JSON che i residui NVMe
            }
        }
    }
}