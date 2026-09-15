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
using System.Xml;
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

        private long GetAvailableRamMB()
        {
            try
            {
                if (File.Exists("/proc/meminfo"))
                {
                    string[] lines = File.ReadAllLines("/proc/meminfo");
                    long memFree = 0;
                    long buffers = 0;
                    long cached = 0;
                    long memAvailable = -1;

                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemAvailable:"))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1 && long.TryParse(parts[1], out long val)) memAvailable = val / 1024;
                        }
                        else if (line.StartsWith("MemFree:"))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1 && long.TryParse(parts[1], out long val)) memFree = val / 1024;
                        }
                        else if (line.StartsWith("Buffers:"))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1 && long.TryParse(parts[1], out long val)) buffers = val / 1024;
                        }
                        else if (line.StartsWith("Cached:"))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1 && long.TryParse(parts[1], out long val)) cached = val / 1024;
                        }
                    }

                    if (memAvailable != -1) return memAvailable;
                    return memFree + buffers + cached;
                }
            }
            catch { }
            return 8000; // Default fallback
        }

        private async Task<int> ProcessAndIngestTextChunkAsync(string chatId, string fileName, string fullText, int chunkIndexOffset = 0)
        {
            int chunkSize = 1200;
            int overlap = 100;
            var chunks = new List<string>();
            
            for (int i = 0; i < fullText.Length; i += (chunkSize - overlap))
            {
                int length = Math.Min(chunkSize, fullText.Length - i);
                chunks.Add(fullText.Substring(i, length));
                if (i + length >= fullText.Length) break;
            }

            int chunkIndex = chunkIndexOffset;
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
            return chunkIndex;
        }

        public async Task IngestDocumentAsync(string chatId, string filePath, Action<string> logCallback, IProgress<(int currentBatch, int totalBatches, int currentPage, int totalPages, string status)>? progress = null)
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
                        int totalPages = document.NumberOfPages;
                        
                        if (totalPages > 50)
                        {
                            logCallback($"[FALDONE]: ⚠️ Rilevato documento esteso ({totalPages} pagine). Attivata modalità Streaming a lotti Zero-OOM.");
                            int batchSize = 30;
                            int totalBatches = (int)Math.Ceiling(totalPages / (double)batchSize);
                            int chunkIndexOffset = 0;
                            
                            for (int currentBatch = 1; currentBatch <= totalBatches; currentBatch++)
                            {
                                long freeRam = GetAvailableRamMB();
                                if (freeRam < 4000)
                                {
                                    logCallback($"[FALDONE ERRORE]: Memoria di sicurezza superata ({freeRam} MB liberi). Sospensione ingestione per prevenire OOM.");
                                    throw new Exception("RAM insufficiente per continuare l'ingestione.");
                                }

                                int startPage = (currentBatch - 1) * batchSize + 1;
                                int endPage = Math.Min(currentBatch * batchSize, totalPages);
                                
                                progress?.Report((currentBatch, totalBatches, endPage, totalPages, "Estrazione testo..."));

                                string batchText = "";
                                for (int p = startPage; p <= endPage; p++)
                                {
                                    batchText += document.GetPage(p).Text + "\n";
                                }

                                if (!string.IsNullOrWhiteSpace(batchText))
                                {
                                    progress?.Report((currentBatch, totalBatches, endPage, totalPages, "Generazione vettori..."));
                                    chunkIndexOffset = await ProcessAndIngestTextChunkAsync(chatId, fileName, batchText, chunkIndexOffset);
                                }

                                logCallback($"[FALDONE]: Ingestione lotto {currentBatch}/{totalBatches} completata (Pagine {startPage}-{endPage}).");

                                batchText = null;
                                GC.Collect(2, GCCollectionMode.Forced, true, true);
                                GC.WaitForPendingFinalizers();
                            }
                            
                            logCallback($"[FALDONE]: Ingestione massiva conclusa con successo.");
                            return; // Completato via streaming
                        }
                        else
                        {
                            foreach (var page in document.GetPages())
                            {
                                fullText += page.Text + "\n";
                            }
                        }
                    }
                }
                else if (extension == ".docx")
                {
                    long fileLength = new FileInfo(filePath).Length;
                    if (fileLength > 1024 * 1024)
                    {
                        logCallback($"[FALDONE]: ⚠️ Rilevato archivio DOCX esteso ({fileLength / 1024 / 1024} MB). Attivata modalità Streaming XML Zero-OOM.");
                        
                        using (var archive = ZipFile.OpenRead(filePath))
                        {
                            var entry = archive.Entries.FirstOrDefault(e => e.FullName == "word/document.xml");
                            if (entry != null)
                            {
                                using (var stream = entry.Open())
                                using (var reader = XmlReader.Create(stream))
                                {
                                    int chunkIndexOffset = 0;
                                    int batchCounter = 1;
                                    var sb = new StringBuilder();
                                    string overlapText = "";
                                    
                                    while (reader.Read())
                                    {
                                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "p")
                                        {
                                            string paraText = reader.ReadInnerXml();
                                            paraText = Regex.Replace(paraText, "<[^>]*>", " ");
                                            if (!string.IsNullOrWhiteSpace(paraText))
                                            {
                                                sb.AppendLine(paraText.Trim());
                                            }
                                        }

                                        if (sb.Length >= 50000)
                                        {
                                            long freeRam = GetAvailableRamMB();
                                            if (freeRam < 4000) throw new Exception("RAM insufficiente.");

                                            string batchText = overlapText + sb.ToString();
                                            
                                            double percent = ((double)stream.Position / entry.Length) * 100;
                                            progress?.Report((batchCounter, -1, (int)percent, 100, $"Analisi DOCX XML ({percent:F1}%)"));

                                            chunkIndexOffset = await ProcessAndIngestTextChunkAsync(chatId, fileName, batchText, chunkIndexOffset);
                                            
                                            if (batchText.Length > 100) overlapText = batchText.Substring(batchText.Length - 100);
                                            else overlapText = "";
                                            
                                            sb.Clear();
                                            batchCounter++;
                                            
                                            GC.Collect(2, GCCollectionMode.Forced, true, true);
                                            GC.WaitForPendingFinalizers();
                                        }
                                    }
                                    
                                    if (sb.Length > 0)
                                    {
                                        string batchText = overlapText + sb.ToString();
                                        await ProcessAndIngestTextChunkAsync(chatId, fileName, batchText, chunkIndexOffset);
                                    }
                                }
                            }
                        }
                        logCallback($"[FALDONE]: Ingestione massiva DOCX conclusa con successo.");
                        return;
                    }
                    else
                    {
                        using (var archive = ZipFile.OpenRead(filePath))
                        {
                            var entry = archive.Entries.FirstOrDefault(e => e.FullName == "word/document.xml");
                            if (entry != null)
                            {
                                using (var stream = entry.Open())
                                using (var streamReader = new StreamReader(stream))
                                {
                                    string xml = await streamReader.ReadToEndAsync();
                                    fullText = Regex.Replace(xml, "<[^>]*>", " ");
                                }
                            }
                        }
                    }
                }
                else if (extension == ".txt" || extension == ".md" || extension == ".csv" || extension == ".json" || extension == ".log")
                {
                    long fileLength = new FileInfo(filePath).Length;
                    if (fileLength > 1024 * 1024)
                    {
                        logCallback($"[FALDONE]: ⚠️ Rilevato file testo esteso ({fileLength / 1024 / 1024} MB). Attivata modalità Streaming a lotti Zero-OOM.");
                        
                        using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        using (var streamReader = new StreamReader(fs, Encoding.UTF8, true, 8192))
                        {
                            int batchSizeThreshold = 50000;
                            var sb = new StringBuilder(batchSizeThreshold + 1000);
                            int chunkIndexOffset = 0;
                            int batchCounter = 1;
                            string overlapText = "";
                            bool isEndOfStream = false;

                            while (!isEndOfStream)
                            {
                                sb.Append(overlapText);
                                int readChars = 0;
                                
                                while (readChars < batchSizeThreshold)
                                {
                                    string? line = await streamReader.ReadLineAsync();
                                    if (line == null)
                                    {
                                        isEndOfStream = true;
                                        break;
                                    }
                                    sb.AppendLine(line);
                                    readChars += line.Length + 1;
                                }
                                
                                if (sb.Length == 0) break;

                                long freeRam = GetAvailableRamMB();
                                if (freeRam < 4000) throw new Exception("RAM insufficiente.");
                                
                                string batchText = sb.ToString();
                                double percent = ((double)fs.Position / fileLength) * 100;
                                progress?.Report((batchCounter, -1, (int)percent, 100, $"Elaborazione testo ({fs.Position / 1024 / 1024} MB / {fileLength / 1024 / 1024} MB)"));

                                chunkIndexOffset = await ProcessAndIngestTextChunkAsync(chatId, fileName, batchText, chunkIndexOffset);
                                
                                if (batchText.Length > 100 && !isEndOfStream)
                                    overlapText = batchText.Substring(batchText.Length - 100);
                                else
                                    overlapText = "";

                                sb.Clear();
                                batchCounter++;

                                GC.Collect(2, GCCollectionMode.Forced, true, true);
                                GC.WaitForPendingFinalizers();
                            }
                        }
                        logCallback($"[FALDONE]: Ingestione massiva testuale conclusa con successo.");
                        return;
                    }
                    else
                    {
                        fullText = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                    }
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

                logCallback($"[FALDONE]: Testo estratto. Calcolo vettori e ingestione...");
                await ProcessAndIngestTextChunkAsync(chatId, fileName, fullText);
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
