using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace OperaSuprema.Core
{
    public class AutonomousCrawler
    {
        private readonly VectorMemoryManager _vectorMemory;
        private readonly HttpClient _httpClient;

        public AutonomousCrawler(VectorMemoryManager vectorMemory)
        {
            _vectorMemory = vectorMemory;
            _httpClient = new HttpClient();
            // Camuffamento "Browser Fantasma" per non farci riconoscere come Bot C#
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
        }

        public async Task StartTrainingAsync(string mainTopic, List<string> searchQueries, int maxPagesPerQuery, Action<string> logCallback)
        {
            logCallback($"[RAGNO]: 🕷️ Inizio missione di addestramento su '{mainTopic}'. Chiavi di ricerca ricevute: {searchQueries.Count}");
            int totalChunksMemorized = 0;

            foreach (var query in searchQueries)
            {
                logCallback($"[RAGNO]: 🔍 Interrogo i server (DDG Lite / Bing) per: '{query}'...");
                try
                {
                    await Task.Delay(2000); // Ritardo umano per non far scattare gli allarmi

                    // TENTATIVO 1: L'ingresso segreto (DuckDuckGo Lite via POST)
                    string searchUrl = "https://lite.duckduckgo.com/lite/";
                    var postData = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("q", query) });
                    var response = await _httpClient.PostAsync(searchUrl, postData);

                    // Se ci bloccano anche qui, passiamo istantaneamente a Bing
                    if (!response.IsSuccessStatusCode)
                    {
                        logCallback($"[RAGNO SWAP]: 🔄 DDG Lite ha rifiutato l'accesso. Switch tattico su Bing Search...");
                        searchUrl = $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}";
                        response = await _httpClient.GetAsync(searchUrl);
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        logCallback($"[RAGNO BLOCCO]: 🛑 Entrambi i motori di ricerca ci hanno bloccato. Passo alla query successiva...");
                        continue;
                    }

                    string htmlContent = await response.Content.ReadAsStringAsync();
                    
                    // Regex Universale: prende tutti i link veri ignorando la spazzatura
                    var linkMatches = Regex.Matches(htmlContent, @"<a[^>]+href=""(?<url>https?://[^""]+)""");
                    var uniqueUrls = new HashSet<string>();

                    foreach (Match m in linkMatches)
                    {
                        string u = m.Groups["url"].Value;
                        
                        // Pulizia esistente
                        if (u.Contains("duckduckgo.com") || u.Contains("bing.com") || u.Contains("microsoft.com") || u.Contains("msn.com") || u.Contains("youtube.com") || u.Contains("facebook.com") || u.Contains("twitter.com")) continue;
                        
                        // --- FIX ANTI-COMA: Ignora PDF e archivi binari ---
                        // --- SCUDO ANTI-COMA: Ignora tutto ciò che non è pagina web o PDF ---
                        if (u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                            u.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) || 
                            u.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || 
                            u.EndsWith(".doc", StringComparison.OrdinalIgnoreCase) || 
                            u.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) || 
                            u.EndsWith(".xls", StringComparison.OrdinalIgnoreCase) || 
                            u.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)) 
                        {
                            continue;
                        }
                        
                        // Se c'è un tracking DDG...
                        if (u.Contains("uddg=")) u = Uri.UnescapeDataString(u.Split("uddg=")[1].Split("&")[0]);

                        uniqueUrls.Add(u);
                    }

                    if (uniqueUrls.Count == 0)
                    {
                        logCallback($"[RAGNO AVVISO]: ⚠️ Nessun link utile trovato nella pagina. Probabile blocco Cloudflare.");
                        continue;
                    }

                    int pagesScraped = 0;
                    foreach (var url in uniqueUrls)
                    {
                        if (pagesScraped >= maxPagesPerQuery) break;

                        logCallback($"[RAGNO]: 📥 Tentativo di estrazione da: {url}");
                        try
                        {
                            await Task.Delay(1500); // Non sovraccarichiamo i server bersaglio

                            var pageResponse = await _httpClient.GetAsync(url);
                            if (pageResponse.IsSuccessStatusCode)
                            {
                                string cleanText = "";
                                
                                // CONTROLLO INTELLIGENTE: È una pagina Web o un PDF?
                                if (url.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || 
                                   (pageResponse.Content.Headers.ContentType != null && pageResponse.Content.Headers.ContentType.MediaType == "application/pdf"))
                                {
                                    logCallback($"[RAGNO]: 📄 Rilevato documento PDF. Avvio estrazione strutturale da {url}...");
                                    try
                                    {
                                        byte[] pdfBytes = await pageResponse.Content.ReadAsByteArrayAsync();
                                        using (var document = PdfDocument.Open(pdfBytes))
                                        {
                                            foreach (var page in document.GetPages())
                                            {
                                                cleanText += page.Text + " ";
                                            }
                                        }
                                        cleanText = Regex.Replace(cleanText, @"\s+", " ").Trim();
                                        logCallback($"[RAGNO]: 📜 Estratti {cleanText.Length} caratteri dal PDF.");
                                    }
                                    catch (Exception pdfEx)
                                    {
                                        logCallback($"[RAGNO SKIP]: ⚠️ Errore lettura PDF protetto o corrotto ({url}): {pdfEx.Message}");
                                        continue;
                                    }
                                }
                                else
                                {
                                    // È una normale pagina web, usa il vecchio sistema di pulizia HTML
                                    string rawHtml = await pageResponse.Content.ReadAsStringAsync();
                                    cleanText = Regex.Replace(rawHtml, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
                                    cleanText = Regex.Replace(cleanText, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
                                    cleanText = Regex.Replace(cleanText, @"<[^>]+>", " ");
                                    cleanText = Regex.Replace(cleanText, @"\s+", " ").Trim();
                                }

                                if (cleanText.Length > 500)
                                {
                                    int chunkSize = 1000; 
                                    for (int i = 0; i < cleanText.Length; i += chunkSize)
                                    {
                                        string chunk = cleanText.Substring(i, Math.Min(chunkSize, cleanText.Length - i));
                                        string metadataTag = $"[TRAINING: {mainTopic}] [FONTE: {url}] [PARTE: {(i / chunkSize) + 1}]";
                                        
                                        // 1. Facciamo partire il cronometro
                                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                                        
                                        // 2. Aspettiamo che Nomic dica "Ho finito"
                                        await _vectorMemory.MemorizeContentAsync(metadataTag, chunk);
                                        totalChunksMemorized++;
                                        
                                        // 3. Fermiamo il cronometro
                                        stopwatch.Stop();
                                        
                                        // 4. SENSORE DI SFORZO (Adaptive Throttling)
                                        // Se Nomic ci ha messo più di 1.5 secondi per un blocco da 1000 caratteri, la GPU è in affanno.
                                        if (stopwatch.ElapsedMilliseconds > 1500)
                                        {
                                            // Diamo 1 secondo di respiro per abbassare le temperature
                                            await Task.Delay(1000); 
                                        }
                                        else
                                        {
                                            // La GPU è veloce e fredda, avanti il prossimo all'istante!
                                            await Task.Delay(10); 
                                        }
                                    }
                                    pagesScraped++;
                                    logCallback($"[RAGNO SUCCESS]: ✅ Testo cristallizzato da {url}");
                                }
                                else
                                {
                                    logCallback($"[RAGNO SKIP]: ⚠️ Sito senza contenuto testuale o protetto ({url})");
                                }
                            }
                            else
                            {
                                logCallback($"[RAGNO BLOCCO]: 🛡️ Accesso negato dal sito (Errore {pageResponse.StatusCode}) - {url}");
                            }
                        }
                        catch (Exception ex)
                        {
                            logCallback($"[RAGNO ERRORE CRITICO]: ❌ Fallimento su {url} -> {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logCallback($"[RAGNO ERRORE MOTORE DI RICERCA]: {ex.Message}");
                }
            }

            logCallback($"[ACCADEMIA]: 🎓 Addestramento completato. {totalChunksMemorized} frammenti di conoscenza cristallizzati nel database vettoriale per uso futuro.");
        }
    }
}