using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OperaSuprema.Core.Memory;

namespace OperaSuprema.Core.Infrastructure
{
    public class SessionDossierExporter
    {
        public static async Task ExportDossierToStreamAsync(StreamWriter writer, ChatSession session, DecisionLedgerService ledgerService, SessionDocumentManager docManager)
        {
            await writer.WriteLineAsync($"# 🏛️ DOSSIER ESECUTIVO DI SESSIONE: {session.Title}");
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("## 📌 METADATI OPERATIVI");
            await writer.WriteLineAsync($"- **ID Sessione:** `{session.Id}`");
            await writer.WriteLineAsync($"- **Data Esportazione:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            await writer.WriteLineAsync($"- **Workspace di riferimento:** {session.Title}");
            await writer.WriteLineAsync();

            await writer.WriteLineAsync("## 📂 DOCUMENTI E CORPORE ANALIZZATI");
            var docs = await docManager.GetSessionDocumentNamesAsync(session.Id);
            if (docs != null && docs.Any())
            {
                foreach (var doc in docs)
                {
                    await writer.WriteLineAsync($"- 📄 `{doc}`");
                }
            }
            else
            {
                await writer.WriteLineAsync("*Nessun documento nel faldone di sessione.*");
            }
            await writer.WriteLineAsync();

            await writer.WriteLineAsync("## ⚖️ REGISTRO DELLE DECISIONI ATTIVE");
            var allDecisions = await ledgerService.GetAllDecisionsAsync();
            var activeDecisions = allDecisions.Where(d => d.Status == "Active").ToList();
            if (activeDecisions.Any())
            {
                await writer.WriteLineAsync("| Soggetto | Predicato | Oggetto | Razionale |");
                await writer.WriteLineAsync("|---|---|---|---|");
                foreach (var dec in activeDecisions)
                {
                    string safeSubject = dec.Subject?.Replace("|", "-").Replace("\n", " ") ?? "";
                    string safePredicate = dec.Predicate?.Replace("|", "-").Replace("\n", " ") ?? "";
                    string safeObject = dec.ObjectValue?.Replace("|", "-").Replace("\n", " ") ?? "";
                    string safeRationale = dec.Rationale?.Replace("|", "-").Replace("\n", " ") ?? "-";
                    
                    await writer.WriteLineAsync($"| {safeSubject} | {safePredicate} | {safeObject} | {safeRationale} |");
                }
            }
            else
            {
                await writer.WriteLineAsync("*Nessuna decisione attiva registrata nel Ledger.*");
            }
            await writer.WriteLineAsync();

            await writer.WriteLineAsync("## 🧠 VERDETTI E SINTESI CHIAVE");
            var keyInsights = session.Messages.Where(m => m.Role == "model" && m.Content != null && m.Content.Length > 200).TakeLast(3).ToList();
            if (keyInsights.Any())
            {
                foreach (var msg in keyInsights)
                {
                    await writer.WriteLineAsync("> " + msg.Content?.Replace("\n", "\n> "));
                    await writer.WriteLineAsync();
                }
            }
            else
            {
                await writer.WriteLineAsync("*Nessuna sintesi estesa presente in questa sessione.*");
            }
            await writer.WriteLineAsync();

            await writer.WriteLineAsync("## 📜 CRONISTORIA ANALITICA");
            foreach (var msg in session.Messages)
            {
                string roleIcon = msg.Role == "user" ? "👤 **Utente:**" : "🤖 **Master Mentor:**";
                await writer.WriteLineAsync($"{roleIcon}");
                await writer.WriteLineAsync(msg.Content);
                await writer.WriteLineAsync();
                await writer.WriteLineAsync("---");
                await writer.WriteLineAsync();
            }
        }
    }
}
