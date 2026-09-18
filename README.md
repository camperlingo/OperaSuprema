# 🌌 Opera Suprema - Local Enterprise AI Agentic IDE

[![Version](https://img.shields.io/badge/version-v2.0--certified-blue.svg)](https://github.com/camperlingo/OperaSuprema)
[![Platform](https://img.shields.io/badge/platform-Linux%20%7C%20Windows%20%7C%20macOS-lightgrey.svg)](https://github.com/camperlingo/OperaSuprema)
[![Tech](https://img.shields.io/badge/tech-.NET%2010%20%7C%20Avalonia%20UI%2011-purple.svg)](https://github.com/camperlingo/OperaSuprema)
[![Architecture](https://img.shields.io/badge/architecture-Dual--LLM%20%7C%20MoE%20%7C%20Agentic-orange.svg)](https://github.com/camperlingo/OperaSuprema)

[🇬🇧 English](#-english) | [🇮🇹 Italiano](#-italiano)

---

## 🇬🇧 English

Opera Suprema is a cross-platform, local Enterprise AI IDE engineered to orchestrate multi-agent neural architectures completely offline. Built with C#, .NET 10, and Avalonia UI, it functions as a centralized autonomous engineering workshop without relying on proprietary cloud APIs.

### 🌌 The Paradigm of the Fall (Our Manifesto)
In Opera Suprema, a build error is not a failure; it is fuel. This IDE introduces a self-evolving engineering loop: if code fails during compilation or runtime, the system captures stderr/stdout, enriches context with past failure memory, queries web documentation via local scrapers, fixes the code, and permanently crystallizes the solution into vector storage.

### ⚡ Key Capabilities
* **Dual-LLM Engineering Orchestration:** High-level architectural planning via Gemma 3 27B (Master Mentor) paired with dedicated 30B code synthesis (Qwen Coder) on isolated ports.
* **Autonomous IDE & Self-Healing Pipeline:** Automated generation of .NET/Avalonia solutions from scratch, filesystem projection with anti-collision retry patterns, real-time dotnet build diagnosis, and zero-error recovery loops.
* **Knowledge Atlas (Atlante della Conoscenza):** Multimodal zero-human corpus ingestion, automatic discipline classification, semantic Qdrant indexing, and on-demand discipline pruning.
* **Decision Ledger & Relational Memory:** SQLite with WAL mode and FTS5 full-text search enforcing active architectural rules across sessions with deterministic prefix caching.
* **Linux Kernel Supervision:** Full process tree management (process.Kill(true)) preventing orphan processes, zombie MSBuild daemons, and port locks.
* **Zero-LOH Streaming:** Low-level HTTP payload streaming avoiding Large Object Heap fragmentation during multi-megabyte code and multimodal tensor transfers.
* **Deterministic System Commands:** Integrated quick access for /blueprint, /addestra, and /decisioni.

---

## 🇮🇹 Italiano

Opera Suprema è un IDE Enterprise avanzato e multipiattaforma per l'orchestrazione locale di architetture neurali multi-agente. Sviluppato in C#, .NET 10 e Avalonia UI, agisce come un laboratorio autonomo di ingegneria del software completamente offline.

### 🌌 Il Paradigma della Caduta (Il Nostro Manifesto)
In Opera Suprema, un errore del compilatore non è una sconfitta, ma carburante. Questo IDE introduce un ecosistema auto-evolutivo: quando il codice fallisce, il sistema analizza il log degli errori, consulta la memoria dei fallimenti passati, estrae documentazione aggiornata via web scraping, corregge la struttura e cristallizza permanentemente la soluzione nel database vettoriale.

### ⚡ Funzionalità Principali
* **Orchestrazione Dual-LLM:** Separazione netta tra pianificazione strategica (Gemma 3 27B - Master Mentor) e implementazione del codice (Qwen 30B - Coder Principale).
* **Pipeline IDE Autonoma e Self-Healing:** Generazione automatica di progetti Avalonia/.NET, scrittura sicura su disco con Retry Pattern anti-collisione, ciclo di auto-compilazione con dotnet build e correzione zero-shot.
* **Atlante della Conoscenza Specialistica:** Ingestione autonoma di documenti (PDF/testo), classificazione semantica della disciplina senza intervento umano e indicizzazione vettoriale su Qdrant.
* **Decision Ledger su SQLite WAL:** Registro relazionale con FTS5 per imporre decisioni architetturali vincolanti e preservare la coerenza tecnica nel tempo.
* **Supervisione Processi Kernel Linux:** Tracciamento rigoroso del ciclo di vita dei processi generati da dotnet run, azzerando i processi orfani e i file lock.
* **Gestione Memoria Zero-LOH:** Streaming JSON a basso livello per evitare la frammentazione della Large Object Heap durante il passaggio di file sorgente e tensori multimediali.
* **Comandi Rapidi di Sistema:** Menu rapido e terminale interattivo per /blueprint, /addestra e /decisioni.

---

## 🛠️ Requisiti di Sistema & Avvio

1. .NET 10 SDK installato.
2. Qdrant Vector Database attivo su localhost:6333.
3. llama.cpp / llama-server compilato per il proprio acceleratore.
4. Esegui il boot:
   dotnet run