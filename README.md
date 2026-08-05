# 🎭 Opera Suprema - Local Enterprise AI IDE

![Version](https://img.shields.io/badge/version-v8.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)
![Tech Stack](https://img.shields.io/badge/Tech-C%23%20%7C%20Avalonia%20UI-purple.svg)

🇬🇧 **[English](#english)** | 🇮🇹 **[Italiano](#italiano)**

---

<a name="english"></a>
## 🇬🇧 English

**Opera Suprema** is an advanced, cross-platform Local Enterprise AI IDE designed to orchestrate multi-agent neural architectures completely offline. Built with C# and Avalonia UI, it acts as a centralized brain to manage local Mixture of Experts (MoE) workflows without relying on cloud services.

### 🚀 Core Features
*   **Multi-Agent Orchestration:** Simultaneously run and manage specialized models (e.g., Master Mentor, Cybersec Coder, VisionJak, and Embedding Engine) via dedicated local ports.
*   **Smart VRAM Optimization:** Features an intelligent "Hot-Swapping" system with granular memory offload sliders (0-100%). It dynamically monitors system RAM (supporting high-capacity CAMM2/SO-DIMM setups) and VRAM to prevent Out-Of-Memory errors.
*   **Native `llama.cpp` Integration:** Seamlessly hooks into the `llama-server` executable, automatically traversing directories to find the correct binaries for your hardware (CUDA/ROCm/Metal/AVX2).
*   **Hardware-Aware:** Designed to fully saturate high-bandwidth connections like OCuLink (x8 PCIe 5.0) for rapid context switching between heavy LLMs.
*   **Remote Management:** Built-in Telegram Bot integration for secure remote monitoring and directive execution.

### 🛠️ Getting Started
1. Clone the repository.
2. Ensure you have the `.NET SDK` installed.
3. Download the correct `llama-server` binary for your hardware from the official `llama.cpp` repository.
4. Run `dotnet run` in the project root.
5. On first launch, the IDE will prompt you to link your `llama-server` executable and configure your GGUF models.

---

<a name="italiano"></a>
## 🇮🇹 Italiano

**Opera Suprema** è un IDE Enterprise avanzato e multipiattaforma per l'orchestrazione locale di architetture neurali multi-agente. Scritto in C# e basato su Avalonia UI, funge da "cervello" centralizzato per gestire flussi di lavoro Mixture of Experts (MoE) completamente in locale, senza appoggiarsi a servizi cloud.

### 🚀 Funzionalità Principali
*   **Orchestrazione Multi-Agente:** Gestisci ed esegui simultaneamente modelli specializzati (es. Master Mentor, Coder, VisionJak, Embedding Engine) tramite porte locali dedicate.
*   **Ottimizzazione VRAM Intelligente:** Sistema di "Hot-Swapping" dinamico con slider percentuali (0-100%) per il controllo granulare dell'offload. Il radar integrato monitora la RAM reale di sistema (ideale per configurazioni ad altissima capacità CAMM2 o SO-DIMM) per evitare blocchi e saturazioni.
*   **Integrazione Nativa `llama.cpp`:** Si aggancia in modo trasparente all'eseguibile `llama-server`, ricercando autonomamente i binari corretti per l'hardware in uso (CUDA/ROCm/Metal/AVX2).
*   **Hardware-Aware:** Progettato per sfruttare al massimo la banda passante di connessioni come OCuLink (x8 PCIe 5.0), garantendo un rapido context switching tra LLM pesanti.
*   **Gestione da Remoto:** Integrazione Telegram Bot per il monitoraggio sicuro e l'invio di direttive anche a distanza.

### 🛠️ Installazione
1. Clona la repository.
2. Assicurati di avere installato il `.NET SDK`.
3. Scarica l'eseguibile `llama-server` corretto per il tuo hardware dalla repository ufficiale di `llama.cpp`.
4. Esegui `dotnet run` nella cartella principale.
5. Al primo avvio, l'interfaccia ti guiderà nel collegamento dell'eseguibile e nella configurazione dei tuoi modelli GGUF.

---
*Opera Suprema - Your logic, your hardware, your rules.*