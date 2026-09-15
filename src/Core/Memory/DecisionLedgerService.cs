using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace OperaSuprema.Core.Memory;

public class DecisionLedgerService
{
    private readonly string _dbPath;
    private readonly string _connectionString;

    public DecisionLedgerService()
    {
        var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "OperaSuprema");
        if (!Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }
        
        _dbPath = Path.Combine(configDir, "opera_memory.db");
        _connectionString = $"Data Source={_dbPath};Mode=ReadWriteCreate;Cache=Shared;Default Timeout=5;";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var walCommand = connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 5000;";
        walCommand.ExecuteNonQuery();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS chat_messages (
                message_id TEXT PRIMARY KEY,
                chat_id TEXT NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                token_count INTEGER NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS memory_entities (
                entity_id TEXT PRIMARY KEY,
                subject TEXT NOT NULL,
                predicate TEXT NOT NULL,
                object_value TEXT NOT NULL,
                ref_count INTEGER DEFAULT 1,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(subject, predicate, object_value)
            );

            CREATE TABLE IF NOT EXISTS entity_provenance (
                entity_id TEXT NOT NULL,
                chat_id TEXT NOT NULL,
                message_id TEXT,
                PRIMARY KEY (entity_id, chat_id),
                FOREIGN KEY(entity_id) REFERENCES memory_entities(entity_id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS decision_ledger (
                decision_id TEXT PRIMARY KEY,
                entity_id TEXT NOT NULL,
                status TEXT NOT NULL,
                superseded_by_id TEXT,
                rationale TEXT,
                confidence REAL DEFAULT 1.0,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY(entity_id) REFERENCES memory_entities(entity_id) ON DELETE CASCADE
            );

            -- FTS5 Tables
            CREATE VIRTUAL TABLE IF NOT EXISTS chat_messages_fts USING fts5(
                content,
                chat_id UNINDEXED,
                content='chat_messages',
                content_rowid='rowid'
            );

            CREATE VIRTUAL TABLE IF NOT EXISTS decision_ledger_fts USING fts5(
                subject,
                predicate,
                object_value,
                rationale,
                decision_id UNINDEXED,
                status UNINDEXED
            );

            -- Triggers for chat_messages_fts
            CREATE TRIGGER IF NOT EXISTS chat_messages_ai AFTER INSERT ON chat_messages BEGIN
                INSERT INTO chat_messages_fts(rowid, content, chat_id) VALUES (new.rowid, new.content, new.chat_id);
            END;
            CREATE TRIGGER IF NOT EXISTS chat_messages_ad AFTER DELETE ON chat_messages BEGIN
                INSERT INTO chat_messages_fts(chat_messages_fts, rowid, content, chat_id) VALUES ('delete', old.rowid, old.content, old.chat_id);
            END;
            CREATE TRIGGER IF NOT EXISTS chat_messages_au AFTER UPDATE ON chat_messages BEGIN
                INSERT INTO chat_messages_fts(chat_messages_fts, rowid, content, chat_id) VALUES ('delete', old.rowid, old.content, old.chat_id);
                INSERT INTO chat_messages_fts(rowid, content, chat_id) VALUES (new.rowid, new.content, new.chat_id);
            END;

            -- Triggers for decision_ledger_fts
            DROP TRIGGER IF EXISTS decision_ledger_ad;
            DROP TRIGGER IF EXISTS decision_ledger_au;

            CREATE TRIGGER IF NOT EXISTS decision_ledger_ai AFTER INSERT ON decision_ledger BEGIN
                INSERT INTO decision_ledger_fts(rowid, subject, predicate, object_value, rationale, decision_id, status)
                SELECT new.rowid, m.subject, m.predicate, m.object_value, new.rationale, new.decision_id, new.status
                FROM memory_entities m WHERE m.entity_id = new.entity_id;
            END;

            CREATE TRIGGER IF NOT EXISTS decision_ledger_ad AFTER DELETE ON decision_ledger BEGIN
                DELETE FROM decision_ledger_fts WHERE rowid = old.rowid;
            END;

            CREATE TRIGGER IF NOT EXISTS decision_ledger_au AFTER UPDATE ON decision_ledger BEGIN
                DELETE FROM decision_ledger_fts WHERE rowid = old.rowid;
                INSERT INTO decision_ledger_fts(rowid, subject, predicate, object_value, rationale, decision_id, status)
                SELECT new.rowid, m.subject, m.predicate, m.object_value, new.rationale, new.decision_id, new.status
                FROM memory_entities m WHERE m.entity_id = new.entity_id;
            END;
        ";
        command.ExecuteNonQuery();
    }

    public async Task InsertChatMessageAsync(string messageId, string chatId, string role, string content, int tokenCount)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO chat_messages (message_id, chat_id, role, content, token_count)
            VALUES (@msgId, @chatId, @role, @content, @tokenCount);
        ";
        command.Parameters.AddWithValue("@msgId", messageId);
        command.Parameters.AddWithValue("@chatId", chatId);
        command.Parameters.AddWithValue("@role", role);
        command.Parameters.AddWithValue("@content", content);
        command.Parameters.AddWithValue("@tokenCount", tokenCount);

        await command.ExecuteNonQueryAsync();
    }

    public async Task RegisterOrUpdateDecisionAsync(ExtractedTriple triple, string chatId, string? messageId = null)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            // Upsert memory_entity
            command.CommandText = @"
                INSERT INTO memory_entities (entity_id, subject, predicate, object_value)
                VALUES (@entityId, @sub, @pred, @obj)
                ON CONFLICT(subject, predicate, object_value) DO UPDATE SET ref_count = ref_count + 1
                RETURNING entity_id;
            ";
            string newEntityId = Guid.NewGuid().ToString();
            command.Parameters.AddWithValue("@entityId", newEntityId);
            command.Parameters.AddWithValue("@sub", triple.Subject);
            command.Parameters.AddWithValue("@pred", triple.Predicate);
            command.Parameters.AddWithValue("@obj", triple.ObjectValue);
            
            var resolvedEntityId = (await command.ExecuteScalarAsync())?.ToString();
            
            // Insert provenance
            if (resolvedEntityId != null)
            {
                command.CommandText = @"
                    INSERT OR IGNORE INTO entity_provenance (entity_id, chat_id, message_id)
                    VALUES (@resEntityId, @chatId, @msgId);
                ";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@resEntityId", resolvedEntityId);
                command.Parameters.AddWithValue("@chatId", chatId);
                command.Parameters.AddWithValue("@msgId", messageId ?? (object)DBNull.Value);
                await command.ExecuteNonQueryAsync();

                string newDecisionId = Guid.NewGuid().ToString();

                // Sostituiamo la logica con un aggiornamento diretto e sicuro per bloccare la concorrenza
                command.CommandText = @"
                    UPDATE decision_ledger 
                    SET status = 'Superseded', superseded_by_id = @newDecId 
                    WHERE entity_id IN (
                        SELECT entity_id FROM memory_entities WHERE subject = @sub AND predicate = @pred
                    ) AND status = 'Active';
                ";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@newDecId", newDecisionId);
                command.Parameters.AddWithValue("@sub", triple.Subject);
                command.Parameters.AddWithValue("@pred", triple.Predicate);
                await command.ExecuteNonQueryAsync();

                // Inseriamo la nuova decisione attiva
                command.CommandText = @"
                    INSERT INTO decision_ledger (decision_id, entity_id, status, rationale, confidence)
                    VALUES (@newDecId, @resEntityId, 'Active', @rationale, @conf);
                ";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@newDecId", newDecisionId);
                command.Parameters.AddWithValue("@resEntityId", resolvedEntityId);
                command.Parameters.AddWithValue("@rationale", triple.Rationale ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@conf", triple.Confidence);
                await command.ExecuteNonQueryAsync();
            }
            
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteChatSafelyAsync(string chatId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;

            // Get entities to decrement
            command.CommandText = "SELECT entity_id FROM entity_provenance WHERE chat_id = @chatId;";
            command.Parameters.AddWithValue("@chatId", chatId);
            
            var entitiesToDecrement = new System.Collections.Generic.List<string>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    entitiesToDecrement.Add(reader.GetString(0));
                }
            }

            // Remove provenance
            command.CommandText = "DELETE FROM entity_provenance WHERE chat_id = @chatId;";
            await command.ExecuteNonQueryAsync();

            // Decrement ref_count and delete if <= 0
            foreach (var entityId in entitiesToDecrement)
            {
                command.CommandText = @"
                    UPDATE memory_entities 
                    SET ref_count = ref_count - 1 
                    WHERE entity_id = @entId;
                    
                    DELETE FROM memory_entities 
                    WHERE entity_id = @entId AND ref_count <= 0;
                ";
                command.Parameters.Clear();
                command.Parameters.AddWithValue("@entId", entityId);
                await command.ExecuteNonQueryAsync();
            }

            // Delete chat messages
            command.CommandText = "DELETE FROM chat_messages WHERE chat_id = @chatId;";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@chatId", chatId);
            await command.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<System.Collections.Generic.List<(string Subject, string Predicate, string ObjectValue, string Status, string? Rationale)>> GetAllDecisionsAsync()
    {
        var list = new System.Collections.Generic.List<(string, string, string, string, string?)>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT m.subject, m.predicate, m.object_value, d.status, d.rationale 
            FROM decision_ledger d 
            JOIN memory_entities m ON d.entity_id = m.entity_id 
            ORDER BY d.status ASC, d.created_at DESC;
        ";
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)
            ));
        }
        return list;
    }
}
