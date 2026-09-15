using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using System.IO;

namespace OperaSuprema.Core.Memory;

public class MemoryRouter
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "that", "this", "from", "are", "have", "not", "but", "was",
        "they", "will", "what", "can", "out", "about", "who", "get", "which", "go", "me", "when",
        "make", "can", "like", "time", "no", "just", "him", "know", "take", "people", "into", "year",
        "your", "good", "some", "could", "them", "see", "other", "than", "then", "now", "look",
        "only", "come", "its", "over", "think", "also", "back", "after", "use", "two", "how", "our",
        "work", "first", "well", "way", "even", "new", "want", "because", "any", "these", "give",
        "day", "most", "us", "il", "lo", "la", "i", "gli", "le", "un", "uno", "una", "di", "a", "da",
        "in", "con", "su", "per", "tra", "fra", "che", "non", "si", "mi", "ti", "ci", "vi", "ne"
    };

    private readonly string _connectionString;

    public MemoryRouter()
    {
        var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "OperaSuprema", "opera_memory.db");
        _connectionString = $"Data Source={dbPath};Mode=ReadWrite;Cache=Shared;";
    }

    public IReadOnlyList<string> FastExtractKeywords(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return Array.Empty<string>();

        var matches = Regex.Matches(input, @"\b[a-zA-Z0-9]{3,30}\b");
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in matches)
        {
            var word = match.Value.ToLowerInvariant();
            if (!StopWords.Contains(word))
            {
                keywords.Add(word);
            }
        }

        return keywords.ToList();
    }

    public async Task<ContextPayload> RouteAndAssembleAsync(RetrievalQuery query)
    {
        var keywords = query.ExtractedKeywords.Any() ? query.ExtractedKeywords : FastExtractKeywords(query.RawText);
        
        var decisions = new List<DecisionRecord>();
        var snippets = new List<string>();
        
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // 0. Estrarre SEMPRE le decisioni Active come verità assoluta
        using (var aCommand = connection.CreateCommand())
        {
            aCommand.CommandText = @"
                SELECT d.decision_id, d.entity_id, d.status, d.superseded_by_id, m.subject, m.predicate, m.object_value, d.rationale, d.confidence, d.created_at
                FROM decision_ledger d
                JOIN memory_entities m ON d.entity_id = m.entity_id
                WHERE d.status = 'Active'
                ORDER BY d.created_at ASC;
            ";
            using (var reader = await aCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    decisions.Add(new DecisionRecord(
                        reader.GetString(0),
                        reader.GetString(1),
                        Enum.Parse<DecisionStatus>(reader.GetString(2)),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        reader.GetString(4),
                        reader.GetString(5),
                        reader.GetString(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7),
                        reader.GetDouble(8),
                        reader.GetDateTime(9)
                    ));
                }
            }
        }

        if (keywords.Any())
        {
            string ftsQueryStr = string.Join(" OR ", keywords.Select(k => $"\"{k}\"*"));

            // 1. Search in decision_ledger_fts for superseded decisions IF requested
            if (query.IncludeSuperseded)
            {
                using (var dCommand = connection.CreateCommand())
                {
                    dCommand.CommandText = @"
                        SELECT d.decision_id, d.entity_id, d.status, d.superseded_by_id, m.subject, m.predicate, m.object_value, d.rationale, d.confidence, d.created_at
                        FROM decision_ledger d
                        JOIN memory_entities m ON d.entity_id = m.entity_id
                        WHERE d.decision_id IN (
                            SELECT decision_id FROM decision_ledger_fts 
                            WHERE decision_ledger_fts MATCH @ftsQuery
                        )
                        AND d.status = 'Superseded';
                    ";
                    dCommand.Parameters.AddWithValue("@ftsQuery", ftsQueryStr);

                    using (var reader = await dCommand.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            decisions.Add(new DecisionRecord(
                                reader.GetString(0),
                                reader.GetString(1),
                                Enum.Parse<DecisionStatus>(reader.GetString(2)),
                                reader.IsDBNull(3) ? null : reader.GetString(3),
                                reader.GetString(4),
                                reader.GetString(5),
                                reader.GetString(6),
                                reader.IsDBNull(7) ? null : reader.GetString(7),
                                reader.GetDouble(8),
                                reader.GetDateTime(9)
                            ));
                        }
                    }
                }
            }

            // 2. Search in chat_messages_fts
            using (var cCommand = connection.CreateCommand())
            {
                cCommand.CommandText = @"
                    SELECT content FROM chat_messages_fts 
                    WHERE chat_messages_fts MATCH @ftsQuery 
                    AND chat_id = @chatId
                    LIMIT 5;
                ";
                cCommand.Parameters.AddWithValue("@ftsQuery", ftsQueryStr);
                cCommand.Parameters.AddWithValue("@chatId", query.ChatId);

                using (var reader = await cCommand.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        snippets.Add(reader.GetString(0));
                    }
                }
            }
        }

        // Assemble Deterministic Prefix Block
        var activeDecisions = decisions.Where(d => d.Status == DecisionStatus.Active)
                                       .OrderBy(d => d.Subject)
                                       .ThenBy(d => d.Predicate)
                                       .ToList();

        var sb = new StringBuilder();
        string header = "### ARCHITECTURE ACTIVE DECISIONS (SYSTEM ENFORCED)\n";
        sb.Append(header);
        
        int estimatedTokens = header.Length / 4;
        
        var includedDecisions = new List<DecisionRecord>();

        foreach (var decision in activeDecisions)
        {
            string line = $"- {decision.Subject} [{decision.Predicate}] {decision.ObjectValue}\n";
            int lineTokens = line.Length / 4;
            
            if (estimatedTokens + lineTokens > query.TokenBudget)
            {
                break;
            }
            
            sb.Append(line);
            estimatedTokens += lineTokens;
            includedDecisions.Add(decision);
        }

        activeDecisions = includedDecisions;
        string deterministicBlock = sb.ToString();

        return new ContextPayload(activeDecisions, snippets, deterministicBlock, estimatedTokens);
    }
}
