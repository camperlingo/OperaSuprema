using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OperaSuprema.Core 
{
    // 1. Definiamo come è fatto un singolo messaggio
    public class ChatMessage
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
    }

    // 2. Definiamo l'intero "Faldone" (La sessione della chat)
    public class ChatSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Nuova Conversazione";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; } = false;
        public string? ProjectPath { get; set; } // <--- Il magico punto di domanda
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    // 3. Il Motore che gestisce i file fisici
    public class SessionManager
    {
        private readonly string _genericChatsPath;

        public SessionManager()
        {
            // Crea la directory nascosta di default nel sistema Linux dell'utente
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            _genericChatsPath = Path.Combine(homePath, ".config", "OperaSuprema", "Chats");
            Directory.CreateDirectory(_genericChatsPath);
            
            // All'avvio, lancia lo spazzino per le chat temporanee vecchie
            CleanupOldGenericChats();
        }

        // Funzione per salvare la chat su disco
        public void SaveSession(ChatSession session, string? currentProjectPath = null) // <--- E un altro qui
        {
            session.LastModified = DateTime.Now;
            session.ProjectPath = currentProjectPath;

            string saveDir = string.IsNullOrEmpty(currentProjectPath) 
                ? _genericChatsPath 
                : Path.Combine(currentProjectPath, ".nexus", "chats");
            
            Directory.CreateDirectory(saveDir); // Assicura che la cartella esista
            
            string filePath = Path.Combine(saveDir, $"{session.Id}.json");
            string json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            
            File.WriteAllText(filePath, json);
        }

        // Funzione per caricare TUTTE le chat (Generiche + Quelle del progetto attuale)
        public List<ChatSession> LoadAllAvailableSessions(string? currentProjectPath = null)
        {
            var sessions = new List<ChatSession>();

            // 1. Carica le chat generiche
            sessions.AddRange(LoadSessionsFromDirectory(_genericChatsPath));

            // 2. Se c'è un progetto caricato, carica anche i faldoni di quel progetto
            if (!string.IsNullOrEmpty(currentProjectPath))
            {
                string projectChatsDir = Path.Combine(currentProjectPath, ".nexus", "chats");
                if (Directory.Exists(projectChatsDir))
                {
                    sessions.AddRange(LoadSessionsFromDirectory(projectChatsDir));
                }
            }

            // Ordina: Prima le fissate (Pinned), poi le più recenti
            return sessions.OrderByDescending(s => s.IsPinned)
                           .ThenByDescending(s => s.LastModified)
                           .ToList();
        }

        private List<ChatSession> LoadSessionsFromDirectory(string directoryPath)
        {
            var list = new List<ChatSession>();
            foreach (var file in Directory.GetFiles(directoryPath, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var session = JsonSerializer.Deserialize<ChatSession>(json);
                    if (session != null) list.Add(session);
                }
                catch { /* Ignora i file corrotti */ }
            }
            return list;
        }

        // Funzioni Operative per i tre puntini (Rinomina, Elimina, Fissa)
        public void DeleteSession(ChatSession session)
        {
            string dir = string.IsNullOrEmpty(session.ProjectPath) ? _genericChatsPath : Path.Combine(session.ProjectPath, ".nexus", "chats");
            string file = Path.Combine(dir, $"{session.Id}.json");
            if (File.Exists(file)) File.Delete(file);
        }

        public void RenameSession(ChatSession session, string newTitle)
        {
            session.Title = newTitle;
            SaveSession(session, session.ProjectPath);
        }

        public void TogglePinSession(ChatSession session)
        {
            session.IsPinned = !session.IsPinned;
            SaveSession(session, session.ProjectPath);
        }

        // Lo spazzino automatico (elimina le chat generiche > 7 giorni se non fissate)
        private void CleanupOldGenericChats()
        {
            var oldSessions = LoadSessionsFromDirectory(_genericChatsPath)
                .Where(s => !s.IsPinned && (DateTime.Now - s.LastModified).TotalDays > 7);

            foreach (var session in oldSessions)
            {
                DeleteSession(session);
            }
        }
    }
}