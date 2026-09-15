using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace OperaSuprema.Core.Infrastructure
{
    public class SessionDocumentManager
    {
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private readonly VectorMemoryManager _vectorManager;
        
        private const string QdrantBaseUrl = "http://localhost:6333";
        private const string CollectionName = "opera_session_documents";
        private const int VectorDimensions = 768;
        
        private bool _collectionInitialized = false;

        public SessionDocumentManager(VectorMemoryManager vectorManager)
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
                    Console.WriteLine($"[QDRANT DOCS] Creazione collection '{CollectionName}'...");
                    
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
                        Console.WriteLine($"[QDRANT DOCS] Collection creata con successo.");
                    else
                    {
                        string error = await createResponse.Content.ReadAsStringAsync();
                        Console.WriteLine($"[QDRANT DOCS ERRORE] Creazione fallita: {error}");
                    }
                }
                
                _collectionInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QDRANT DOCS ERRORE] Impossibile inizializzare: {ex.Message}");
            }
        }

        public async Task IngestDocumentAsync(string chatId, string filePath, Action<string> logCallback)
        {
            await EnsureCollectionExistsAsync();
            
            string fileName = Path.GetFileName(filePath);
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            string fullText = "";

            logCallback($"[FALDONE]: Estrazione testo da {fileName} in corso...");

            try
            {
                if (extension == ".pdf")
                {
                    using (PdfDocument document = PdfDocument.Open(filePath))
                    {
                        foreach (var page in document.GetPages())
                        {
                            fullText += page.Text + "\n";
                        }
                    }
                }
                else if (extension == ".docx")
                {
                    using (var archive = ZipFile.OpenRead(filePath))
                    {
                        var entry = archive.Entries.FirstOrDefault(e => e.FullName == "word/document.xml");
                        if (entry != null)
                        {
                            using (var stream = entry.Open())
                            using (var reader = new StreamReader(stream))
                            {
                                string xml = await reader.ReadToEndAsync();
                                fullText = Regex.Replace(xml, "<[^>]*>", " ");
                            }
                        }
                    }
                }
                else if (extension == ".txt" || extension == ".md" || extension == ".csv" || extension == ".json")
                {
                    fullText = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                }
                else
                {
                    throw new NotSupportedException($"Formato non supportato: {extension}");
                }

                if (string.IsNullOrWhiteSpace(fullText))
                {
                    logCallback($"[FALDONE]: {fileName} non contiene testo estraibile.");
                    return;
                }

                // Chunking (1200 chars, 100 overlap)
                int chunkSize = 1200;
                int overlap = 100;
                var chunks = new List<string>();
                
                for (int i = 0; i < fullText.Length; i += (chunkSize - overlap))
                {
                    int length = Math.Min(chunkSize, fullText.Length - i);
                    chunks.Add(fullText.Substring(i, length));
                    if (i + length >= fullText.Length) break;
                }

                logCallback($"[FALDONE]: Testo estratto. Calcolo vettori per {chunks.Count} blocchi...");

                int chunkIndex = 0;
                foreach (var chunk in chunks)
                {
                    float[] vector = await _vectorManager.GetEmbeddingAsync(chunk, false);
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
                                    chat_id = chatId,
                                    file_name = fileName,
                                    chunk_index = chunkIndex,
                                    content = chunk,
                                    timestamp = DateTime.UtcNow.Ticks
                                }
                            }
                        }
                    };

                    var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PutAsync($"{QdrantBaseUrl}/collections/{CollectionName}/points", requestContent);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"[QDRANT DOCS] Errore inserimento chunk {chunkIndex}");
                    }
                    chunkIndex++;
                }

                logCallback($"[FALDONE]: Documento '{fileName}' ingerito e vettorializzato con successo.");
            }
            catch (Exception ex)
            {
                logCallback($"[FALDONE ERRORE]: Fallita ingestione di {fileName}: {ex.Message}");
            }
        }

        public async Task<List<string>> SearchSessionDocsAsync(string chatId, string query, int topK = 4)
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
                    with_payload = true,
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "chat_id",
                                match = new { value = chatId }
                            }
                        }
                    }
                };

                var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{QdrantBaseUrl}/collections/{CollectionName}/points/search", requestContent);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using (var doc = JsonDocument.Parse(jsonResponse))
                    {
                        if (doc.RootElement.TryGetProperty("result", out var resultList))
                        {
                            foreach (var item in resultList.EnumerateArray())
                            {
                                if (item.TryGetProperty("payload", out var pld) && 
                                    pld.TryGetProperty("content", out var contentElement) &&
                                    pld.TryGetProperty("file_name", out var fileNameElement))
                                {
                                    results.Add($"[DOC: {fileNameElement.GetString()}] {contentElement.GetString()}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QDRANT DOCS ERRORE] Errore di ricerca: {ex.Message}");
            }

            return results;
        }

        public async Task DeleteSessionDocsAsync(string chatId)
        {
            try
            {
                var payload = new
                {
                    filter = new
                    {
                        must = new[]
                        {
                            new
                            {
                                key = "chat_id",
                                match = new { value = chatId }
                            }
                        }
                    }
                };

                var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{QdrantBaseUrl}/collections/{CollectionName}/points/delete")
                {
                    Content = requestContent
                };
                
                var response = await _httpClient.SendAsync(request);
                
                if (response.IsSuccessStatusCode)
                    Console.WriteLine($"[FALDONE] Documenti per chat {chatId} eliminati da Qdrant.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QDRANT DOCS ERRORE] Eliminazione documenti fallita: {ex.Message}");
            }
        }
    }
}
