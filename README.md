# 🎭 Opera Suprema - Local Enterprise AI Agentic IDE

![Version](https://img.shields.io/badge/version-v9.0-blue.svg)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-lightgrey.svg)
![Tech Stack](https://img.shields.io/badge/Tech-C%23%20%7C%20Avalonia%20UI-purple.svg)
![Architecture](https://img.shields.io/badge/Architecture-Multi--Agent%20%7C%20RAG-success.svg)

🇬🇧 **[English](#english)** | 🇮🇹 **[Italiano](#italiano)**

---

<a name="english"></a>
## 🇬🇧 English

**Opera Suprema** is an advanced, cross-platform Local Enterprise AI IDE designed to orchestrate multi-agent neural architectures completely offline[cite: 4]. Built with C# and Avalonia UI, it acts as a centralized brain to manage local Mixture of Experts (MoE) workflows without relying on cloud services[cite: 4].

### 🌌 The Paradigm of the Fall (Our Manifesto)
In Opera Suprema, a crash is not a failure; it is fuel. This IDE introduces a self-evolving ecosystem where the AI learns from its mistakes. If the compiler crashes, the system analyzes the error, searches the web for modern documentation, fixes the code, and permanently crystallizes the solution in its Vector Hippocampus (Qdrant). *Do not fear the red terminal: pushing the system to its limits is how it achieves perfection.*

### 🚀 Core Features
*   **Hub & Spoke Agentic Workflow:** An Architect agent designs the logic, a Coder agent blindly executes it, and a Supreme Judge evaluates the output. A flawless, continuous self-healing loop.
*   **Vector Hippocampus (RAG):** Uses deterministic MD5 hashing to store successes and failures in a local vector database. The AI remembers past bugs and avoids repeating them.
*   **Autonomous Web Hound:** Local models are no longer limited by their training cutoff. The IDE autonomously scrapes the web to learn new framework syntaxes (e.g., Avalonia 11.1 updates) and applies them in real-time.
*   **Smart VRAM Optimization:** Features an intelligent "Hot-Swapping" system with granular memory offload sliders (0-100%)[cite: 4]. It dynamically monitors system RAM (supporting high-capacity CAMM2/SO-DIMM setups) and VRAM to prevent Out-Of-Memory errors[cite: 4].
*   **Native `llama.cpp` Integration:** Seamlessly hooks into the `llama-server` executable, automatically traversing directories to find the correct binaries for your hardware (CUDA/ROCm/Metal/AVX2)[cite: 4].
*   **Hardware-Aware:** Designed to fully saturate high-bandwidth connections like OCuLink (x8 PCIe 5.0) for rapid context switching between heavy LLMs[cite: 4].
*   **Remote Management:** Built-in Telegram Bot integration for secure remote monitoring and directive execution[cite: 4].

### 🛠️ Getting Started
1. Clone the repository.
2. Ensure you have the `.NET SDK` installed.
3. Download the correct `llama-server` binary for your hardware from the official `llama.cpp` repository[cite: 4].
4. Run `dotnet run` in the project root[cite: 4].
5. On first launch, the IDE will prompt you to link your `llama-server` executable and configure your GGUF models[cite: 4].

---

<a name="italiano"></a>
## 🇮🇹 Italiano

**Opera Suprema** è un IDE Enterprise avanzato e multipiattaforma per l'orchestrazione locale di architetture neurali multi-agente[cite: 4]. Scritto in C# e basato su Avalonia UI, funge da "cervello" centralizzato per gestire flussi di lavoro Mixture of Experts (MoE) completamente in locale, senza appoggiarsi a servizi cloud[cite: 4].

### 🌌 Il Paradigma della Caduta (Il Nostro Manifesto)
In Opera Suprema, un errore del compilatore non è un fallimento, è carburante. Questo IDE introduce un ecosistema auto-evolutivo in cui l'IA impara dai propri errori. Se il codice va in crash, il sistema analizza il log, interroga il web alla ricerca di documentazione aggiornata, applica la correzione e cristallizza permanentemente la soluzione nel suo Ippocampo Vettoriale (Qdrant). *Non temete gli errori a terminale: spingere il sistema al limite è l'unico modo per renderlo infallibile.*

### 🚀 Funzionalità Principali
*   **Flusso Agentico "Hub & Spoke":** Un agente Architetto progetta la logica, un agente Coder la esegue e un Giudice Supremo valuta il codice. Un ciclo di auto-guarigione perfetto e continuo.
*   **Ippocampo Vettoriale (RAG):** Utilizza un sistema di ID deterministici (MD5) per memorizzare fallimenti e successi in un database vettoriale locale. L'IA ricorda i vecchi bug e non li ripete mai più.
*   **Segugio Web Autonomo:** I modelli locali non sono più limitati alla data del loro addestramento. L'IDE raschia autonomamente il web per imparare nuove sintassi (es. aggiornamenti di Avalonia 11.1) e applicarle in tempo reale.
*   **Ottimizzazione VRAM Intelligente:** Sistema di "Hot-Swapping" dinamico con slider percentuali (0-100%) per il controllo granulare dell'offload[cite: 4]. Il radar integrato monitora la RAM reale di sistema (ideale per configurazioni ad altissima capacità CAMM2 o SO-DIMM) per evitare blocchi e saturazioni[cite: 4].
*   **Integrazione Nativa `llama.cpp`:** Si aggancia in modo trasparente all'eseguibile `llama-server`, ricercando autonomamente i binari corretti per l'hardware in uso (CUDA/ROCm/Metal/AVX2)[cite: 4].
*   **Hardware-Aware:** Progettato per sfruttare al massimo la banda passante di connessioni come OCuLink (x8 PCIe 5.0), garantendo un rapido context switching tra LLM pesanti[cite: 4].
*   **Gestione da Remoto:** Integrazione Telegram Bot per il monitoraggio sicuro e l'invio di direttive anche a distanza[cite: 4].

### 🛠️ Installazione
1. Clona la repository.
2. Assicurati di avere installato il `.NET SDK`.
3. Scarica l'eseguibile `llama-server` corretto per il tuo hardware dalla repository ufficiale di `llama.cpp`[cite: 4].
4. Esegui `dotnet run` nella cartella principale[cite: 4].
5. Al primo avvio, l'interfaccia ti guiderà nel collegamento dell'eseguibile e nella configurazione dei tuoi modelli GGUF[cite: 4].

---
*Opera Suprema - Your logic, your hardware, your rules.*