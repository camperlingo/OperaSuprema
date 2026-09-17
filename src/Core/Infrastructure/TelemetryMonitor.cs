using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OperaSuprema.Core.Infrastructure
{
    public class TelemetryData
    {
        public double RamTotalGb { get; set; }
        public double RamAvailableGb { get; set; }
        public double RamUsedPercent { get; set; }
        public double CpuLoadPercent { get; set; }
        
        public bool MasterMentorOnline { get; set; }
        public bool CoderOnline { get; set; }
        public bool VisionJakOnline { get; set; }
        public bool AudioJakOnline { get; set; }
        public bool EmbeddingOnline { get; set; }
        
        public bool QdrantOnline { get; set; }
        public int TotalVectors { get; set; }
    }

    public class TelemetryMonitor
    {
        private CancellationTokenSource? _cts;
        private readonly HttpClient _httpClient;
        public event Action<TelemetryData>? OnTelemetryUpdated;

        public TelemetryMonitor()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        }

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => PollingLoopAsync(_cts.Token));
        }

        public void Stop()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }

        private async Task PollingLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var data = new TelemetryData();

                    // 1. Lettura RAM da /proc/meminfo
                    if (File.Exists("/proc/meminfo"))
                    {
                        var lines = await File.ReadAllLinesAsync("/proc/meminfo", ct);
                        double memTotalKb = 0;
                        double memAvailableKb = 0;

                        foreach (var line in lines)
                        {
                            if (line.StartsWith("MemTotal:"))
                                memTotalKb = ParseMemInfoValue(line);
                            else if (line.StartsWith("MemAvailable:"))
                                memAvailableKb = ParseMemInfoValue(line);
                        }

                        if (memTotalKb > 0)
                        {
                            data.RamTotalGb = memTotalKb / (1024.0 * 1024.0);
                            data.RamAvailableGb = memAvailableKb / (1024.0 * 1024.0);
                            data.RamUsedPercent = ((memTotalKb - memAvailableKb) / memTotalKb) * 100.0;
                        }
                    }

                    // 2. Lettura CPU Load
                    if (File.Exists("/proc/loadavg"))
                    {
                        var loadStr = await File.ReadAllTextAsync("/proc/loadavg", ct);
                        var parts = loadStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0 && double.TryParse(parts[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double load1m))
                        {
                            int coreCount = Environment.ProcessorCount;
                            data.CpuLoadPercent = Math.Min(100.0, (load1m / coreCount) * 100.0);
                        }
                    }

                    // 3. Status LLM Containers
                    data.MasterMentorOnline = await CheckLlamaHealthAsync(8081, ct);
                    data.CoderOnline = await CheckLlamaHealthAsync(8082, ct);
                    data.VisionJakOnline = await CheckLlamaHealthAsync(8084, ct);
                    data.AudioJakOnline = await CheckLlamaHealthAsync(8085, ct);
                    data.EmbeddingOnline = await CheckLlamaHealthAsync(8089, ct);

                    // 4. Status Qdrant
                    data.QdrantOnline = false;
                    data.TotalVectors = 0;
                    try
                    {
                        var qResp = await _httpClient.GetAsync("http://localhost:6333/collections", ct);
                        if (qResp.IsSuccessStatusCode)
                        {
                            data.QdrantOnline = true;
                            string qJson = await qResp.Content.ReadAsStringAsync(ct);
                            using var doc = JsonDocument.Parse(qJson);
                            
                            var collections = doc.RootElement.GetProperty("result").GetProperty("collections");
                            int totalPoints = 0;
                            foreach (var collection in collections.EnumerateArray())
                            {
                                string colName = collection.GetProperty("name").GetString() ?? "";
                                var infoResp = await _httpClient.GetAsync($"http://localhost:6333/collections/{colName}", ct);
                                if (infoResp.IsSuccessStatusCode)
                                {
                                    string infoJson = await infoResp.Content.ReadAsStringAsync(ct);
                                    using var infoDoc = JsonDocument.Parse(infoJson);
                                    if (infoDoc.RootElement.TryGetProperty("result", out var res) && res.TryGetProperty("points_count", out var pointsProp))
                                    {
                                        totalPoints += pointsProp.GetInt32();
                                    }
                                }
                            }
                            data.TotalVectors = totalPoints;
                        }
                    }
                    catch { }

                    OnTelemetryUpdated?.Invoke(data);
                }
                catch (Exception)
                {
                    // Evita crash del polling loop
                }

                await Task.Delay(2000, ct);
            }
        }

        private double ParseMemInfoValue(string line)
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && double.TryParse(parts[1], out double val))
                return val;
            return 0;
        }

        private async Task<bool> CheckLlamaHealthAsync(int port, CancellationToken ct)
        {
            try
            {
                var resp = await _httpClient.GetAsync($"http://localhost:{port}/health", ct);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
