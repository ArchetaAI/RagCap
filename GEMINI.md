RagCap — Roadmap
Overview

RagCap is a portable Retrieval-Augmented Generation capsule format.
This document tracks roadmap phases, current progress, and future goals.

🌐 Project Vision
RagCap is a portable Retrieval-Augmented Generation capsule format.
It is an open-source, standalone RAG capsule framework for document ingestion, embedding, and retrieval.

OSS Goal: Allow anyone to build, share, and query portable .ragcap knowledge capsules.

Integration Goal: Ensure RagCap can be seamlessly embedded into my ASP.NET MVC projects while remaining framework-agnostic.

This roadmap is meant for you to understand all the goals of the project and ensure you're working toward completing the project.
We are completing each step one by one, and i will let you know which step we're on:

Phase 1 — Capsule Format & Validation ✅

 Step 1 — Define .ragcap SQLite schema ✅

 Step 2 — Implement manifest validator ✅

Phase 2 — Document Ingestion & Preprocessing ✅

 Step 3 — File loaders (.txt, .md, .pdf, .html) ✅

 Step 4 — Token-aware chunker ✅

 Step 5 — Preprocessors (boilerplate removal, language detection, etc.) ✅

Phase 3 — Embedding Generation 🚧 ✅

 Step 6 — Local embedding support (MiniLM via ONNX) ✅

 Step 7 — Optional API embedding support ✅

Phase 4 — Capsule Building ✅

 Step 8 — ragcap build command ✅

 Step 9 — ragcap inspect command ✅

Phase 5 — Retrieval & Query ✅

 Step 10 — Vector search (cosine similarity) ✅

 Step 11 — BM25 search (SQLite FTS5) ✅

 Step 12 — ragcap search command ✅

Phase 6 — Answer Generation

 Step 13 — Local LLM response generation ✅

 Step 14 — API-based LLM response generation ✅

 Step 15 — ragcap ask command ✅

Phase 7 — Server Mode

 Step 16 — Local HTTP server endpoints ✅ ✅

 Step 17 — ragcap serve command ✅

Phase 8 — Exporting & Interoperability

 Step 18 — Export formats (.parquet, .faiss, .hnsw) 🚧

 Step 19 — Adapters (Python, JavaScript)

Phase 9 — Reproducibility & Comparison

 Step 20 — .ragcap.yml recipe support

 Step 21 — ragcap diff command

Phase 10 — Testing, Packaging & Docs

 Step 22 — Unit tests

 Step 23 — Integration test (end-to-end pipeline)

 Step 24 — Cross-platform packaging

 Step 25 — Docs & examples

Phase 11 — Release

 Step 26 — Tag v0.1 release

Design Principles

CLI-first, portable artifacts

SQLite as the backbone for capsule storage

Pluggable adapters for embeddings, LLMs, and exports

Cross-platform support (.NET, Linux, Windows, macOS)

Stretch Goals / Ideas

Capsule diff visualization in HTML

Capsule chaining for multi-domain RAG

Built-in evaluation suite for RAG performance



ASP.NET MVC Integration Strategy
Now this shouldnt be the focus, but once this project is complete, i need it to seamlessly integrate with ASP.NET MVC project. So dont focus on this too much, as its meant to be OSS-first. Since we already use a .NET framework, it should integrate seamlessly anyway, but i want you to ensure you dont add or change anything that may mess up my end goal of integration. So all logic in RagCap.Core should live in a reusable .NET Standard library.