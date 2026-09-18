using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace OperaSuprema.Core.Infrastructure
{
    public class AtlasKnowledgeManager
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        private readonly VectorMemoryManager _vectorManager;
        
        private const string QdrantBaseUrl = "http://localhost:6333";
        private const string CollectionName = "opera_knowledge_atlas";
        private const int VectorDimensions = 768;
        
        private bool _collectionInitialized = false;

        public AtlasKnowledgeManager(VectorMemoryManager vectorManager)
        {
            _vectorManager = vectorManager;
        }

        public async Task EnsureCollectionExistsAsync()
        {
            if (_collectionInitialized) return;

            try
            {
                var checkResponse = await _httpClient.GetAsync($"{QdrantBaseUrl}/collections/{CollectionName}");
                
                if (!checkResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[QDRANT ATLAS] Creazione collection '{CollectionName}'...");
                    
                    var createPayload = new
                    {
                        vectors = new
                        {
                            size = VectorDimensions,
                            distance = "Cosine"
                        }
                    };

                    var requestContent = System.Net.Http.Json.JsonContent.Create(createPayload);
                    var createResponse = await _httpClient.PutAsync($"{QdrantBaseUrl}/collections/{CollectionName}", requestContent);
                    
                    if (createResponse.IsSuccessStatusCode)
                        Console.WriteLine($"[QDRANT ATLAS] Collection creata con successo.");
                    else
                        Console.WriteLine($"[QDRANT ATLAS] Errore creazione: {await createResponse.Content.ReadAsStringAsync()}");
                }
                
                _collectionInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QDRANT ATLAS EXCEPTION] {ex.Message}");
            }
        }

        private async Task<string> AskLLMForDisciplineAsync(string sampleText)
        {
            try
            {
                var payloadHistory = new List<Dictionary<string, string>>
                {
                    new Dictionary<string, string> { { "role", "system" }, { "content", "Sei un classificatore di testi. Devi rispondere ESCLUSIVAMENTE con una singola parola che rappresenta la disciplina del testo (es. Giurisprudenza, Medicina, Ingegneria, Cybersecurity, Economia, Letteratura). NON aggiungere punteggiatura o commenti." } },
                    new Dictionary<string, string> { { "role", "user" }, { "content", $"CLASSIFICA QUESTO TESTO:\n{sampleText}" } }
                };

                var payload = new { 
                    messages = payloadHistory, 
                    temperature = 0.1, 
                    max_tokens = 50, 
                    stream = false 
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:8081/v1/chat/completions")
                {
                    Content = System.Net.Http.Json.JsonContent.Create(payload)
                };

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var msg = choices[0].GetProperty("message").GetProperty("content").GetString();
                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                // Pulisce da punteggiatura
                                string clean = Regex.Replace(msg.Trim(), @"[^\w\s]", "");
                                string firstWord = clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length > 0 ? clean.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0] : "";
                                if (!string.IsNullOrEmpty(firstWord))
                                {
                                    return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(firstWord.ToLower());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATLAS CLASSIFICATION ERROR]: {ex.Message}");
            }

            return "Generica";
        }

        public async Task<string> ImportCorpusAsync(string filePath, Action<string> logCallback, IProgress<double>? progress = null)
        {
            await EnsureCollectionExistsAsync();
            
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string allText = "";

            try
            {
                // Single-Pass I/O: Legge il contenuto una sola volta
                if (ext == ".pdf")
                {
                    using (var pdf = PdfDocument.Open(filePath))
                    {
                        foreach (var page in pdf.GetPages())
                        {
                            allText += page.Text + "\n";
                        }
                    }
                }
                else
                {
                    allText = await File.ReadAllTextAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                logCallback($"[ATLAS] Errore lettura file: {ex.Message}");
                return "Generica";
            }

            string sampleText = allText.Length > 2000 ? allText.Substring(0, 2000) : allText;
            
            logCallback($"[ATLAS] Analisi Zero-Human Routing in corso per {Path.GetFileName(filePath)}...");
            string discipline = await AskLLMForDisciplineAsync(sampleText);
            logCallback($"[ATLAS] Disciplina autonoma assegnata: {discipline}. Inizio ingestione vettoriale.");

            // Ingestione
            var chunks = SplitIntoChunks(allText, 1500, 300);
            int totalChunks = chunks.Count;

            for (int i = 0; i < totalChunks; i++)
            {
                var chunk = chunks[i];
                string chunkId = Guid.NewGuid().ToString();
                float[] vector = await _vectorManager.GetEmbeddingAsync(chunk);

                var payload = new
                {
                    points = new[]
                    {
                        new
                        {
                            id = chunkId,
                            vector = vector,
                            payload = new
                            {
                                text = chunk,
                                discipline = discipline,
                                filename = Path.GetFileName(filePath),
                                timestamp = DateTime.UtcNow.ToString("o")
                            }
                        }
                    }
                };

                var requestContent = System.Net.Http.Json.JsonContent.Create(payload);
                await _httpClient.PutAsync($"{QdrantBaseUrl}/collections/{CollectionName}/points?wait=true", requestContent);

                if (progress != null)
                {
                    progress.Report((double)(i + 1) / totalChunks * 100);
                }
            }

            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Optimized, false, false);

            logCallback($"[ATLAS] Ingestione di {totalChunks} frammenti completata. Disciplina: {discipline}");
            return discipline;
        }

        public async Task<Dictionary<string, int>> GetAtlasStatsAsync()
        {
            var stats = new Dictionary<string, int>();
            await EnsureCollectionExistsAsync();

            try
            {
                object? offset = null;
                bool hasMore = true;

                while (hasMore)
                {
                    var payload = new Dictionary<string, object>
                    {
                        { "limit", 100 },
                        { "with_payload", true },
                        { "with_vector", false }
                    };

                    if (offset != null) payload["offset"] = offset;

                    var requestContent = System.Net.Http.Json.JsonContent.Create(payload);
                    var response = await _httpClient.PostAsync($"{QdrantBaseUrl}/collections/{CollectionName}/points/scroll", requestContent);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        using (var doc = JsonDocument.Parse(jsonResponse))
                        {
                            if (doc.RootElement.TryGetProperty("result", out var resultObj))
                            {
                                if (resultObj.TryGetProperty("points", out var pointsList))
                                {
                                    foreach (var point in pointsList.EnumerateArray())
                                    {
                                        if (point.TryGetProperty("payload", out var pointPayload) && 
                                            pointPayload.TryGetProperty("discipline", out var discEl))
                                        {
                                            string disc = discEl.GetString() ?? "Sconosciuta";
                                            if (stats.ContainsKey(disc)) stats[disc]++;
                                            else stats[disc] = 1;
                                        }
                                    }
                                }

                                if (resultObj.TryGetProperty("next_page_offset", out var nextOffsetEl) && nextOffsetEl.ValueKind != JsonValueKind.Null)
                                {
                                    if (nextOffsetEl.ValueKind == JsonValueKind.String)
                                        offset = nextOffsetEl.GetString();
                                    else if (nextOffsetEl.ValueKind == JsonValueKind.Number)
                                        offset = nextOffsetEl.GetInt64();
                                    else
                                        offset = nextOffsetEl.ToString();
                                        
                                    if (string.IsNullOrEmpty(offset?.ToString()))
                                        hasMore = false;
                                }
                                else hasMore = false;
                            }
                            else hasMore = false;
                        }
                    }
                    else hasMore = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATLAS STATS ERROR]: {ex.Message}");
            }
            return stats;
        }

        public async Task PruneDisciplineAsync(string discipline)
        {
            await EnsureCollectionExistsAsync();
            try
            {
                var payload = new
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new { key = "discipline", match = new { value = discipline } }
                        }
                    }
                };

                var requestContent = System.Net.Http.Json.JsonContent.Create(payload);
                await _httpClient.PostAsync($"{QdrantBaseUrl}/collections/{CollectionName}/points/delete", requestContent);
                Console.WriteLine($"[ATLAS] Pruning della disciplina {discipline} completato.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATLAS PRUNE ERROR]: {ex.Message}");
            }
        }

        public async Task<List<string>> SearchAtlasAsync(string query, int topK = 4)
        {
            var results = new List<string>();
            await EnsureCollectionExistsAsync();
            try
            {
                float[] queryVector = await _vectorManager.GetEmbeddingAsync(query, true);
                var payload = new
                {
                    vector = queryVector,
                    limit = topK,
                    with_payload = true
                };

                var requestContent = System.Net.Http.Json.JsonContent.Create(payload);
                var response = await _httpClient.PostAsync($"{QdrantBaseUrl}/collections/{CollectionName}/points/search", requestContent);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(jsonResponse);
                    if (doc.RootElement.TryGetProperty("result", out var resultList))
                    {
                        foreach (var item in resultList.EnumerateArray())
                        {
                            if (item.TryGetProperty("payload", out var pld) &&
                                pld.TryGetProperty("text", out var textEl) &&
                                pld.TryGetProperty("discipline", out var discEl) &&
                                pld.TryGetProperty("filename", out var fileEl))
                            {
                                results.Add($"[ATLANTE - {discEl.GetString()} | {fileEl.GetString()}]: {textEl.GetString()}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATLAS SEARCH ERROR]: {ex.Message}");
            }
            return results;
        }

        private List<string> SplitIntoChunks(string text, int maxChunkSize, int overlap)
        {
            var chunks = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return chunks;

            int i = 0;
            while (i < text.Length)
            {
                int length = Math.Min(maxChunkSize, text.Length - i);
                chunks.Add(text.Substring(i, length));
                if (i + length >= text.Length) break;
                i += maxChunkSize - overlap;
            }
            return chunks;
        }
    }
}
