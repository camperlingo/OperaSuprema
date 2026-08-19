using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OperaSuprema.Core
{
    public class StepChatManager
    {
        // Usiamo lo stesso Timeout e BaseUrl di Qdrant
        private static readonly HttpClient _httpClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(120) };
        private const string QdrantBaseUrl = "http://localhost:6333";
        
        // LA NUOVA COLLECTION SU SSD
        private const string ChatCollectionName = "opera_suprema_chat_history";

        // Riferimento al tuo gestore esistente per usare il motore Nomic
        private readonly VectorMemoryManager _vectorManager;
        private bool _collectionInitialized = false;

        /// <summary>
        /// Inietta il VectorMemoryManager per sfruttare il semaforo Nomic già esistente.
        /// </summary>
        public StepChatManager(VectorMemoryManager vectorManager)
        {
            _vectorManager = vectorManager;
        }

        // ==========================================================
        // 1. INIZIALIZZAZIONE DELLA COLLECTION
        // ==========================================================
        public async Task EnsureChatCollectionExistsAsync()
        {
            if (_collectionInitialized) return;

            try
            {
                var checkResponse = await _httpClient.GetAsync($"{QdrantBaseUrl}/collections/{ChatCollectionName}");
                
                if (!checkResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[QDRANT] Creazione nuova collection '{ChatCollectionName}' su SSD per la cronologia chat...");
                    
                    var createPayload = new
                    {
                        vectors = new
                        {
                            size = 768, // Stessa dimensione degli embedding di Nomic
                            distance = "Cosine"
                        }
                    };

                    var requestContent = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");
                    var createResponse = await _httpClient.PutAsync($"{QdrantBaseUrl}/collections/{ChatCollectionName}", requestContent);
                    
                    if (createResponse.IsSuccessStatusCode)
                        Console.WriteLine($"[QDRANT] Collection per gli Step Chat creata con successo.");
                }
                
                _collectionInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[STEP MANAGER ERRORE] Inizializzazione Qdrant fallita: {ex.Message}");
            }
        }

        // ==========================================================
        // 2. MOTORE MATEMATICO ZERO-OOM (CALCOLO BUDGET)
        // ==========================================================
        /// <summary>
        /// Calcola quanti token massimi possiamo idratare dal database senza far esplodere la KV Cache.
        /// </summary>
        public int CalculateContextBudget(int maxContext, int activeChatTokens, int systemPromptTokens = 3000, int outputReserve = 4096)
        {
            // Margine di sicurezza dinamico del 10%
            int safetyMargin = (int)(maxContext * 0.10);

            // T_budget = C_max - (T_blueprint + T_chat_attiva + T_riserva_output + T_margine_sicurezza)
            int availableBudget = maxContext - (systemPromptTokens + activeChatTokens + outputReserve + safetyMargin);

            // Restituisce 0 se lo spazio è esaurito, impedendo al RAG di inviare dati
            return Math.Max(0, availableBudget);
        }

        // ==========================================================
        // 3. SALVATAGGIO DELLO STEP SU DISCO
        // ==========================================================
        public async Task SaveChatStepAsync(string chatId, int stepIndex, string stepContent, string summary)
        {
            await EnsureChatCollectionExistsAsync();

            // Usiamo il GetEmbeddingAsync dal file VectorMemoryManager.cs passando attraverso il suo semaforo!
            float[] vector = await _vectorManager.GetEmbeddingAsync(stepContent, false);
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
                            chat_id = chatId, // Etichetta vitale per rintracciare i file
                            step_index = stepIndex,
                            summary = summary,
                            content = stepContent,
                            timestamp = DateTime.UtcNow.Ticks
                        }
                    }
                }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"{QdrantBaseUrl}/collections/{ChatCollectionName}/points", requestContent);
            
            if (response.IsSuccessStatusCode)
                Console.WriteLine($"[STEP MANAGER] Step {stepIndex} della chat '{chatId}' cristallizzato su disco.");
        }

        // ==========================================================
        // 4. RICERCA SEMANTICA (FILTRATA PER CHAT)
        // ==========================================================
        public async Task<List<string>> RetrieveRelevantStepsAsync(string chatId, string query, int topK = 3)
        {
            var results = new List<string>();
            await EnsureChatCollectionExistsAsync();

            // Calcoliamo il vettore della domanda
            float[] queryVector = await _vectorManager.GetEmbeddingAsync(query, true);

            var payload = new
            {
                vector = queryVector,
                limit = topK,
                with_payload = true,
                // FILTRO MAGICO: Cerca SOLO nei ricordi appartenenti a QUESTA specifica conversazione
                filter = new
                {
                    must = new[]
                    {
                        new { key = "chat_id", match = new { value = chatId } }
                    }
                }
            };

            var requestContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{QdrantBaseUrl}/collections/{ChatCollectionName}/points/search", requestContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonResponse);

                if (doc.RootElement.TryGetProperty("result", out var points))
                {
                    foreach (var point in points.EnumerateArray())
                    {
                        if (point.TryGetProperty("payload", out var matchPayload))
                        {
                            string stepSummary = matchPayload.TryGetProperty("summary", out var sumProp) ? sumProp.GetString() ?? "" : "";
                            string stepContent = matchPayload.TryGetProperty("content", out var contentProp) ? contentProp.GetString() ?? "" : "";
                            
                            results.Add($"--- [MEMORIA A LUNGO TERMINE: {stepSummary}] ---\n{stepContent}");
                        }
                    }
                }
            }
            return results;
        }

        // ==========================================================
        // 5. CANCELLAZIONE DEFINITIVA DALLO STORAGE
        // ==========================================================
        /// <summary>
        /// Quando elimini la chat dall'interfaccia, questo metodo spazza via tutti i dati da Qdrant.
        /// </summary>
        public async Task DeleteChatHistoryAsync(string chatId)
        {
            await EnsureChatCollectionExistsAsync();

            // Diciamo a Qdrant di eliminare tutti i vettori che hanno quel chat_id nel payload
            var payload = new
            {
                filter = new
                {
                    must = new[]
                    {
                        new { key = "chat_id", match = new { value = chatId } }
                    }
                }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, $"{QdrantBaseUrl}/collections/{ChatCollectionName}/points/delete")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[STEP MANAGER] Cronologia della chat '{chatId}' ELIMINATA PERMANENTEMENTE dallo storage NVMe.");
            }
            else
            {
                string error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[STEP MANAGER ERRORE] Impossibile eliminare la chat '{chatId}': {error}");
            }
        }
    }
}