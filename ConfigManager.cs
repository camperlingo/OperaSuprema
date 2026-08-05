using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OperaSuprema.Core
{
    // --- STRUTTURA DATI DEL JSON ---
    public class ModelDefinition
    {
        public string Id { get; set; } = "";
        public string FileName { get; set; } = "";
        public int Port { get; set; }
        public int ContextSize { get; set; }
        public string? MmprojFileName { get; set; } 
        // --- NUOVI PARAMETRI DI OTTIMIZZAZIONE VRAM ---
        public bool UseFlashAttention { get; set; } = true; 
        public string KvCacheType { get; set; } = "q8_0";
	public int GpuOffload { get; set; } = 100;   
    }

    public class AppConfig
    {
        public string StoragePath { get; set; } = "/mnt/AI_Storage/Modelli_GGUF";
        // --- TOGGLE GLOBALE PER IL PING-PONG ---
        public bool HotSwapEnabled { get; set; } = true;
        
        // --- NUOVO: TOKEN TELEGRAM ---
        public string TelegramToken { get; set; } = "";
        
        // --- NUOVO: PERCORSO LLAMA.CPP ---
        public string LlamaServerPath { get; set; } = "";
        
        public Dictionary<string, List<ModelDefinition>> Modes { get; set; } = new();
    }

    // --- MOTORE DI GESTIONE ---
    public class ConfigManager
    {
        private readonly string _configPath = "config.json";
        public AppConfig CurrentConfig { get; private set; }

        public ConfigManager()
        {
            CurrentConfig = LoadConfig();
        }

        private AppConfig LoadConfig()
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    string json = File.ReadAllText(_configPath);
                    return JsonSerializer.Deserialize<AppConfig>(json) ?? GenerateDefaultConfig();
                }
                catch
                {
                    return GenerateDefaultConfig();
                }
            }
            else
            {
                var defaultConfig = GenerateDefaultConfig();
                SaveConfig(defaultConfig); 
                return defaultConfig;
            }
        }

        public void SaveConfig(AppConfig config)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configPath, json);
            CurrentConfig = config; // Aggiorna a caldo la configurazione attiva
        }

        // Reso pubblico per permettere al bottone "Ripristina Default" di chiamarlo
        public AppConfig GenerateDefaultConfig()
        {
            var config = new AppConfig();
            config.HotSwapEnabled = true;

            // Pulizia totale: Teniamo solo i 4 giganti necessari
            config.Modes["HACKER"] = new List<ModelDefinition>
            {
                new ModelDefinition { Id = "EmbeddingEngine", FileName = "nomic-embed-text.gguf", Port = 8089, ContextSize = 8192, UseFlashAttention = false, KvCacheType = "fp16" },
                new ModelDefinition { Id = "MasterMentor_Architetto_Segugio", FileName = "Nidum-gemma-3-27B-it-Uncensored.Q8_0.gguf", Port = 8081, ContextSize = 32768, UseFlashAttention = true, KvCacheType = "q8_0" },
                new ModelDefinition { Id = "VisionJak", FileName = "Qwen2-VL-7B-Q8.gguf", Port = 8084, ContextSize = 16384, MmprojFileName = "mmproj-Qwen2-VL-7B.gguf", UseFlashAttention = true, KvCacheType = "q8_0" },
                new ModelDefinition { Id = "Coder_Principale", FileName = "Huihui-Qwen3-Coder-30B-A3B-Instruct-abliterated.Q8_0.gguf", Port = 8082, ContextSize = 32768, UseFlashAttention = true, KvCacheType = "q8_0" }
            };

            // Copiamo la stessa struttura snella per l'Accademia (Ora con Telegram integrato!)
            config.Modes["ACCADEMIA"] = new List<ModelDefinition>
            {
                new ModelDefinition { Id = "EmbeddingEngine", FileName = "nomic-embed-text.gguf", Port = 8089, ContextSize = 8192, UseFlashAttention = false, KvCacheType = "fp16" },
                new ModelDefinition { Id = "MasterMentor_Architetto_Segugio", FileName = "Nidum-gemma-3-27B-it-Uncensored.Q8_0.gguf", Port = 8081, ContextSize = 32768, UseFlashAttention = true, KvCacheType = "q8_0" },
                new ModelDefinition { Id = "VisionJak", FileName = "Qwen2-VL-7B-Q8.gguf", Port = 8084, ContextSize = 16384, MmprojFileName = "mmproj-Qwen2-VL-7B.gguf", UseFlashAttention = true, KvCacheType = "q8_0" }
            };

            return config;
        }
    }
}