# RagCap
RagCap is an open-source, portable RAG capsule system.  It enables users to ingest, process, and query knowledge bases as single-file capsules,  supporting local and cloud-based embeddings. Designed for CLI-first workflows, RagCap also provides adapters for Python and JavaScript for integration into AI frameworks.

## Usage

The RagCap CLI provides the following commands to build, inspect, and search your knowledge capsules.

### `ragcap build`

Build a new RagCap capsule from a set of source documents.

```bash
ragcap build --input <source_path> --output <capsule_path> [--provider <provider>] [--model <model>]
```

**Arguments:**

*   `--input`: The path to the source documents (a file or a directory).
*   `--output`: The path to the `.ragcap` file to create.
*   `--provider` (optional): The embedding provider to use. Can be `local` or `api`. Defaults to `local`.
*   `--model` (optional): The embedding model to use. For the `local` provider, this is not needed. For the `api` provider, specify the model name.

**Example:**

```bash
# Build a capsule with local embeddings
ragcap build --input ./my_docs --output my_capsule.ragcap

# Build a capsule with API embeddings
export RAGCAP_API_KEY="your_api_key"
ragcap build --input ./my_docs --output my_capsule.ragcap --provider api --model "text-embedding-ada-002"
```

### `ragcap inspect`

Inspect a RagCap capsule to see its metadata and contents.

```bash
ragcap inspect --input <capsule_path> [--json]
```

**Arguments:**

*   `--input`: The path to the `.ragcap` file to inspect.
*   `--json` (optional): Output the result as JSON.

**Example:**

```bash
ragcap inspect --input my_capsule.ragcap
```

### `ragcap search`

Search a RagCap capsule for a given query.

```bash
ragcap search <capsule_path> "<query>" [--top-k <k>] [--mode <mode>] [--json]
```

**Arguments:**

*   `<capsule_path>`: The path to the `.ragcap` file.
*   `<query>`: The search query.
*   `--top-k` (optional): The number of results to return. Defaults to 5.
*   `--mode` (optional): The search mode. Can be `vector`, `bm25`, or `hybrid`. Defaults to `hybrid`.
*   `--json` (optional): Output the result as JSON.

**Example:**

```bash
ragcap search my_capsule.ragcap "what is ragcap?" --top-k 3
```
