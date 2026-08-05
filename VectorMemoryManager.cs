using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading; // Fondamentale per il Semaforo
using System.Threading.Tasks;

namespace OperaSuprema.Core
{
    public class VectorMemoryManager
    {
        private static readonly HttpClient _httpClient = new HttpClient() 
        { 
            // Aumentiamo la pazienza a 2 minuti per i carichi pesanti
            Timeout = TimeSpan.FromSeconds(120) 
        };
        
        // IL SEMAFORO: Lascia passare solo 1 richiesta alla volta per non ingolfare Nomic
        private static readonly SemaphoreSlim _nomicSemaphore = new SemaphoreSlim(1, 1);
        
        private const string EmbeddingUrl = "http://localhost:8089/embedding";
        private const string QdrantBaseUrl = "http://localhost:6333";
        private const string CollectionName = "opera_suprema";
        private const int VectorDimensions = 768;
        
        private bool _collectionInitialized = false;

        public async Task EnsureCollectionExistsAsync()
        {
            if (_collectionInitialized) return;

            try
            {
                var checkResponse = await _httpClient.GetAsync($"{QdrantBaseUrl}/collections/{CollectionName}");
                
                if (!checkResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[QDRANT] Creazione collection '{CollectionName}'...");
                    
                    var createPayload = new
                    {
                        vectors = new
                        {
                            size = VectorDimensions,
                            distance = "Cosine"
                        }
                    };

                    var requestContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
                    var createResponse = await _httpClient.PutAsync($"{QdrantBaseUrl}/collections/{CollectionName}", requestContent);
                    
                    if (createResponse.IsSuccessStatusCode)
                        Console.WriteLine($"[QDRANT] Collection creata con successo.");
                    else
                    {
                        string error = await createResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[QDRANT ERRORE] Creazione fallita: {error}");
                    }
                }
                
                _collectionInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QDRANT ERRORE] Impossibile inizializzare: {ex.Message}");
                throw;
            }
        }

        public async Task<float[]> GetEmbeddingAsync(string text, bool isQuery = false)
        {
            // 1. Applichiamo lo scudo per evitare l'errore 500 (Batch Size limit)
            string safeText = isQuery ? EnsureSafeTokenLimit(text) : text;
            string prefix = isQuery ? "search_query: " : "search_document: ";
            
            // 2. Usiamo 'safeText' (il testo potato) invece di 'text' (il testo originale)!
            string formattedText = prefix + safeText;

            var payload = new { content = formattedText };
            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            // CI METTIAMO IN FILA: Il codice aspetta qui il suo turno
            await _nomicSemaphore.WaitAsync();
            try
            {
                var response = await _httpClient.PostAsync(EmbeddingUrl, requestContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Embedding Error {response.StatusCode}: {errorBody}");
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                float[]? vector = null;
                
                using (var doc = JsonDocument.Parse(jsonResponse))
                {
                    var root = doc.RootElement;
                    
                    if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                    {
                        var firstItem = root[0];
                        if (firstItem.ValueKind == JsonValueKind.Object && firstItem.TryGetProperty("embedding", out var embedProp) && embedProp.ValueKind == JsonValueKind.Array)
                            vector = ExtractFloatArray(embedProp);
                        else if (firstItem.ValueKind == JsonValueKind.Array)
                            vector = ExtractFloatArray(firstItem);
                        else if (firstItem.ValueKind == JsonValueKind.Number)
                            vector = ExtractFloatArray(root);
                    }
                    else if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (root.TryGetProperty("embedding", out var embedProp) && embedProp.ValueKind == JsonValueKind.Array)
                            vector = ExtractFloatArray(embedProp);
                        else if (root.TryGetProperty("data", out var dataProp) && dataProp.ValueKind == JsonValueKind.Array && dataProp.GetArrayLength() > 0)
                        {
                            var firstData = dataProp[0];
                            if (firstData.ValueKind == JsonValueKind.Object && firstData.TryGetProperty("embedding", out var innerEmbed) && innerEmbed.ValueKind == JsonValueKind.Array)
                                vector = ExtractFloatArray(innerEmbed);
                        }
                    }
                }

                if (vector == null || vector.Length == 0)
                    throw new Exception($"Nessun vettore valido estratto da: {jsonResponse}");

                return vector;
            }
            catch (TaskCanceledException)
            {
                throw new Exception("Timeout embedding engine (120s). Nomic sta impiegando troppo tempo per rispondere.");
            }
            finally
            {
                // LIBERIAMO IL SEMAFORO PER IL PROSSIMO CHUNK
                _nomicSemaphore.Release();
            }
        }

        private float[] ExtractFloatArray(JsonElement element)
        {
            var list = new List<float>();
            if (element.ValueKind != JsonValueKind.Array) return list.ToArray();

            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Number)
                    list.Add(item.GetSingle());
                else if (item.ValueKind == JsonValueKind.Array)
                    list.AddRange(ExtractFloatArray(item));
            }
            return list.ToArray();
        }

        public async Task MemorizeContentAsync(string filePath, string content)
        {
            await EnsureCollectionExistsAsync();
            
            // 1. INNESCHIAMO IL CHUNKING: Dividiamo il file lungo in frammenti sicuri
            List<string> chunks = SplitIntoChunks(content);
            int chunkIndex = 0;

            // 2. Salviamo ogni frammento come un punto indipendente nel database
            foreach (var chunk in chunks)
            {
                // Otteniamo l'embedding solo per questo frammento
                float[] vector = await GetEmbeddingAsync(chunk, false);
                string pointId = Guid.NewGuid().ToString();

                var payload = new
                {
                    points = new[]
                    {
                        new
                        {
                            id = pointId,
                            vector = vector,
                            payload = new
                            {
                                path = filePath,
                                chunk_index = chunkIndex, // Salviamo l'indice del frammento
                                code_snippet = chunk, // Memorizziamo solo il frammento, non tutto il file
                                timestamp = DateTime.UtcNow.Ticks
                            }
                        }
                    }
                };

                var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Put, $"{QdrantBaseUrl}/collections/{CollectionName}/points") { Content = requestContent };
                
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Qdrant insert failed on chunk {chunkIndex} of {filePath}: {error}");
                }
                
                chunkIndex++;
            }
            
            Console.WriteLine($"[RAG] File '{filePath}' indicizzato con successo in {chunks.Count} chunk.");
        }

        public async Task<List<string>> SearchContextAsync(string query, int topK = 3)
        {
            var results = new List<string>();
            try
            {
                await EnsureCollectionExistsAsync();
                
                float[] queryVector = await GetEmbeddingAsync(query, true);

                var payload = new
                {
                    vector = queryVector,
                    limit = topK,
                    with_payload = true
                };

                var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{QdrantBaseUrl}/collections/{CollectionName}/points/search", requestContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    string error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[QDRANT ERROR]: Ricerca fallita: {error}");
                    return results; // Restituiamo lista vuota invece di lanciare un'eccezione
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);
                
                if (doc.RootElement.TryGetProperty("result", out var points))
                {
                    foreach (var point in points.EnumerateArray())
                    {
                        if (point.TryGetProperty("payload", out var matchPayload))
                        {
                            string path = matchPayload.TryGetProperty("path", out var pathProp) ? pathProp.GetString() ?? "Sconosciuto" : "Sconosciuto";
                            string code = matchPayload.TryGetProperty("code_snippet", out var codeProp) ? codeProp.GetString() ?? "" : "";
                            results.Add($"--- FILE: {path} ---\n{code}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // L'AIRBAG: Cattura l'errore 500 (o timeout) e salva l'IDE dal crash!
                Console.WriteLine($"[RAG WARNING]: Motore vettoriale temporaneamente in errore o query troncata malamente. Det: {ex.Message}");
                // L'applicazione continuerà a funzionare restituendo 0 risultati dal database locale per questo singolo giro.
            }

            return results;
        }

	// ==========================================================
        // PATCH ARCHITETTURALE: PROTEZIONE OVERFLOW TOKEN
        // ==========================================================
        private string EnsureSafeTokenLimit(string text, int maxChars = 4500)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            
            // 7500 caratteri equivalgono a circa 1800-2000 token.
            // Restiamo sotto la soglia di sicurezza del batch size (2048).
            if (text.Length <= maxChars) return text;
            
            // Se il testo è troppo lungo, lo tronchiamo in modo sicuro
            return text.Substring(0, maxChars);
        }

	// ==========================================================
        // PATCH ARCHITETTURALE: MOTORE DI CHUNKING
        // ==========================================================
        public List<string> SplitIntoChunks(string fullText, int maxCharsPerChunk = 6000)
        {
            var chunks = new List<string>();
            var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var currentChunk = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                if (currentChunk.Length + line.Length > maxCharsPerChunk)
                {
                    chunks.Add(currentChunk.ToString());
                    currentChunk.Clear();
                }
                currentChunk.AppendLine(line);
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString());
            }

            return chunks;
        }
    }
}