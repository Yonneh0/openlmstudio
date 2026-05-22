# llama.cpp Technical Reference Guide

**Repository**: `E:/Projects/AI/llama.cpp/llama-review` (cloned from `https://github.com/ggml-org/llama.cpp`)

This guide covers the major APIs, features, and configuration patterns of the current llama.cpp master branch.

---

## 1. C API Overview (libllama)

The core API is defined in `include/llama.h`. It's a C interface with a C++ header guard.

### Key Types

```c
struct llama_vocab;    // Tokenizer vocabulary
struct llama_model;    // Loaded GGUF model (weights + metadata)
struct llama_context;  // Inference context (KV cache, execution state)
struct llama_sampler;  // Sampling chain (temperature, top-k, etc.)
```

### Model Lifecycle

```c
// Load model (can also load from Hugging Face directly)
llama_model_params model_params = llama_model_default_params();
model_params.n_gpu_layers = 99;  // offload all layers to GPU
model_params.use_mmap = true;    // use memory-mapped I/O
model_params.use_mlock = false;  // force model to stay in RAM
struct llama_model *model = llama_load_model_from_file("model.gguf", model_params);

// Create inference context
llama_context_params ctx_params = llama_context_default_params();
ctx_params.n_ctx = 8192;         // context size
ctx_params.n_batch = 2048;       // logical batch size
ctx_params.n_threads = 8;        // number of CPU threads
ctx_params.n_threads_batch = 8;  // threads for prompt processing
ctx_params.embeddings = false;   // set true for embedding models
struct llama_context *ctx = llama_init_from_model(model, ctx_params);

// Clean up
llama_free(ctx);
llama_free_model(model);
```

### Inference Loop

```c
// Prepare batch
llama_batch batch = llama_batch_get_one(tokens, n_tokens, 0, 0);

// Evaluate
llama_decode(ctx, batch);

// Get logits for last token
float *logits = llama_get_logits(ctx);

// Sample next token
llama_token next_id = llama_sampler_sample(sampler, ctx, -1);

// Feed sampled token back into context
llama_batch batch2 = llama_batch_get_one(&next_id, 1, n_tokens, 0);
llama_decode(ctx, batch2);
```

### Tokenization

```c
// Encode text into token IDs
int32_t n_tokens = llama_tokenize(vocab, "hello world", -1, tokens, MAX_TOKENS, true, false);

// Decode token ID back to text
char buf[256];
int32_t len = llama_token_to_piece(vocab, token_id, buf, sizeof(buf), 0, false);

// Get special token IDs
llama_token bos = llama_vocab_bos(vocab);   // beginning-of-sentence
llama_token eos = llama_vocab_eos(vocab);   // end-of-sentence
llama_token eot = llama_vocab_eot(vocab);   // end-of-turn
llama_token pad = llama_vocab_pad(vocab);   // padding
llama_token fim_pre = llama_vocab_fim_pre(vocab);  // FIM prefix token
llama_token fim_suf = llama_vocab_fim_suf(vocab);  // FIM suffix token
llama_token fim_mid = llama_vocab_fim_mid(vocab);  // FIM insert token
llama_token fim_pad = llama_vocab_fim_pad(vocab);  // FIM pad token
llama_token fim_rep = llama_vocab_fim_rep(vocab);  // FIM repo token
llama_token fim_sep = llama_vocab_fim_sep(vocab);  // FIM separator token
```

### Sampling Chain

```c
// Build a sampling chain (order matters)
llama_sampler *sampler = llama_sampler_chain_init(
    llama_sampler_chain_default_params()
);

llama_sampler_chain_add(sampler, llama_sampler_init_min_p(0.05, 1));
llama_sampler_chain_add(sampler, llama_sampler_init_top_k(40, 1));
llama_sampler_chain_add(sampler, llama_sampler_init_top_p(0.95, 1));
llama_sampler_chain_add(sampler, llama_sampler_init_temp(0.8));
llama_sampler_chain_add(sampler, llama_sampler_init_dist(seed));

// Sample from logits
llama_token id = llama_sampler_sample(sampler, ctx, -1);

llama_sampler_free(sampler);
```

### State / Session Management

```c
// Get state size
size_t state_size = llama_state_get_size(ctx);

// Serialize state
uint8_t *state = malloc(state_size);
size_t written = llama_state_get_data(ctx, state, state_size);

// Restore from file
llama_token *session_tokens = malloc(capacity * sizeof(llama_token));
size_t n_tokens_out;
bool ok = llama_state_load_file(ctx, "session.gguf", session_tokens, capacity, &n_tokens_out);

// Save state
llama_state_save_file(ctx, "session.gguf", session_tokens, n_tokens_out);

// Save to buffer
size_t saved = llama_state_set_data(ctx, state, written);
```

---

## 2. Chat Templates (Jinja2)

### Built-in Templates

llama.cpp ships with the following built-in Jinja2 templates (confirmed from `src/llama-chat.cpp`):

| Template | Template | Template | Template |
|----------|----------|----------|----------|
| chatml | llama2 | llama2-sys | llama2-sys-bos |
| llama2-sys-strip | mistral-v1 | mistral-v3 | mistral-v3-tekken |
| mistral-v7 | mistral-v7-tekken | phi3 | phi4 |
| falcon3 | zephyr | monarch | gemma |
| orion | openchat | vicuna | vicuna-orca |
| deepseek | deepseek2 | deepseek3 | deepseek-ocr |
| command-r | llama3 | chatglm3 | chatglm4 |
| glmedge | minicpm | exaone3 | exaone4 |
| exaone-moe | rwkv-world | granite | granite-4.0 |
| gigachat | megrez | yandex | bailing |
| bailing-think | bailing2 | llama4 | smolvlm |
| hunyuan-moe | gpt-oss | hunyuan-dense | hunyuan-vl |
| kimi-k2 | seed_oss | grok-2 | pangu-embedded |
| solar-open | | | |

Query templates via `--chat-template <name>` (the `-h` flag lists all built-in templates).

### API Usage

```c
// Apply a Jinja template to messages
llama_chat_message chat[] = {
    {"system", "You are a helpful assistant."},
    {"user", "What is 2+2?"}
};
char buf[4096];
int32_t len = llama_chat_apply_template("chatml", chat, 2, true, buf, sizeof(buf));
// len == number of bytes written (may exceed buf size; reallocate and retry)
```

### Server Support

The server endpoint `/apply-template` formats messages without running inference:

```json
POST /apply-template
{
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "Hello"}
  ]
}
// Response: {"prompt": "formatted string..."}
```

### Chat Template Kwargs

The OpenAI-compatible endpoint supports `chat_template_kwargs` to pass parameters to the Jinja templating engine:

```json
{
  "messages": [...],
  "chat_template_kwargs": {
    "enable_thinking": false,
    "reasoning_effort": "high"
  }
}
```

### Model-Defined Templates

Each GGUF model carries its own chat template in GGUF metadata. The server exposes it via `GET /props`:
- `chat_template` — the raw Jinja2 string
- `chat_template_caps` — capabilities of the template

Override with:
```bash
llama-server --chat-template-file my-template.jinja
llama-cli -m model.gguf --chat-template chatml
```

### Model Metadata Access

```c
// Get a metadata value by key
char buf[256];
int32_t len = llama_model_meta_val_str(model, "tokenizer.ggml.add_bos_token", buf, sizeof(buf));

// Iterate all metadata
for (int i = 0; i < llama_model_meta_count(model); i++) {
    char key[256], val[256];
    llama_model_meta_key_by_index(model, i, key, sizeof(key));
    llama_model_meta_val_str_by_index(model, i, val, sizeof(val));
}
```

---

## 3. HTTP Server (llama-server)

### Starting the Server

```bash
llama-server -m model.gguf --port 8080
# or directly from Hugging Face:
llama-server -hf ggml-org/gemma-3-1b-it-GGUF --port 8080
```

### API Key Authentication

```bash
llama-server --api-key sk-your-key-here
```

### OpenAI-Compatible Endpoints

All endpoints under `/v1/*` follow the OpenAI API specification.

#### Chat Completions (`POST /v1/chat/completions`)

```json
{
  "model": "my-model",
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "Hello"}
  ],
  "max_tokens": 1024,
  "stream": true,
  "response_format": {"type": "json_object"},
  "tools": [{"type": "function", "function": {"name": "search"}}]
}
```

Response includes `timings` object with `prompt_n`, `prompt_ms`, `predicted_n`, `predicted_ms`, and `usage` with token counts.

#### Completions (`POST /v1/completions`)

```json
{
  "model": "davinci-002",
  "prompt": "Once upon a time",
  "max_tokens": 256
}
```

#### Embeddings (`POST /v1/embeddings`)

```json
{
  "model": "embedding-model",
  "input": "Hello world",
  "encoding_format": "float"
}
```

#### Messages (Anthropic-Compatible, `POST /v1/messages`)

```json
{
  "model": "claude-3-5-sonnet-20241022",
  "max_tokens": 1024,
  "system": "You are a helpful assistant.",
  "messages": [{"role": "user", "content": "Hello"}],
  "tools": [{"name": "search", "description": "...", "input_schema": {...}}],
  "stream": true
}
```

#### Responses (`POST /v1/responses`)

OpenAI's newer Responses API format — converts internally to Chat Completions.

#### Reranking (`POST /v1/rerank`)

```json
{
  "model": "bge-reranker",
  "query": "What is panda?",
  "documents": ["The panda is a bear species...", "It's a type of cat"]
}
```

### Non-OpenAI Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/health` | GET | Health check (503 while loading) |
| `/completion` | POST | Non-OAI completion with extended options |
| `/tokenize` | POST | Tokenize text (with `with_pieces` option) |
| `/detokenize` | POST | Convert tokens to text |
| `/apply-template` | POST | Apply chat template without inference |
| `/embedding` | POST | Non-OAI embeddings (supports all pooling types) |
| `/infill` | POST | Code infilling with FIM tokens |
| `/reranking` | POST | Document reranking |
| `/models` | GET | List/load/unload models (router mode) |
| `/props` | GET/POST | Server properties and global settings |
| `/slots` | GET | Per-slot metrics and state |
| `/metrics` | GET | Prometheus-compatible metrics |
| `/lora-adapters` | GET/POST | Manage LoRA adapters |
| `/tools` | POST | Built-in agentic tools (internal) |
| `/slots/{id}/save` | POST | Save slot cache to file |
| `/slots/{id}/restore` | POST | Restore slot cache from file |
| `/slots/{id}/erase` | POST | Erase slot cache |

### Server Parameters

Key parameters (full list via `--help`):

```
-m, --model              Model path
--api-key                API key for authentication
--host, --port           Server address (default 127.0.0.1:8080)
--ctx-size, -c           Context size
--batch-size, -b         Logical batch size
--ubatch-size            Physical batch size
--threads, -t            Number of threads
--gpu-layers, --ngl      Layers to offload to GPU (default: auto)
--flash-attn             Flash Attention (auto/on/off)
--kv-offload             KV cache offloading (default: enabled)
--lora                   LoRA adapter path(s)
--jinja                  Enable Jinja templates (required for function calling)
--embedding              Enable embedding endpoint
--reranking              Enable reranking endpoint
--tools all              Enable all built-in agentic tools
--sleep-idle-seconds     Enable auto-sleep mode
--metrics                Enable Prometheus metrics endpoint
--props                  Enable POST /props (change global settings)
--no-mmap                Disable memory-mapped I/O
--mlock                  Keep model in RAM
--log-file               Log to file
--verbose                Verbose logging
```

---

## 4. Built-in Agentic Tools

llama-server includes a set of built-in tools that allow the LLM to access the local file system and other resources directly from the Web UI.

### Enabling Tools

```bash
llama-server --tools all          # Enable all built-in tools
llama-server --tools ls,cat,echo  # Enable specific tools
```

Run `--help` for the full list of available tool names.

### Tool Endpoint

Tools are exposed via the REST endpoint `/tools` (internal API, subject to change):

```json
POST /tools
{
  "name": "ls",
  "arguments": {"path": "/tmp"}
}
```

The Web UI manages tool execution and displays results. **This endpoint is NOT intended for use in downstream applications** — it is an internal implementation detail of the Web UI.

### Function Calling Support

Function calling is supported with the `--jinja` flag. Native tool call formats are supported for:
- Llama 3.1 / 3.3 (including builtin tools: `wolfram_alpha`, `web_search`, `brave_search`, `code_interpreter`)
- Functionary v3.1 / v3.2
- Hermes 2/3, Qwen 2.5
- Qwen 2.5 Coder
- Mistral Nemo
- Firefunction v2
- Command R7B
- DeepSeek R1

Generic tool call format is used as fallback for unrecognized templates (may consume more tokens).

Parallel tool calling is disabled by default; enable with `"parallel_tool_calls": true` in the request body.

---

## 5. MCP (Model Context Protocol) Support

MCP is supported as a **Web UI feature**, not as a server-side API.

### Web UI Architecture

The Web UI manages MCP through several stores:
- `mcpStore` — MCP server definitions, tools, and prompts
- `mcpResourceStore` — MCP resources and attachments
- Per-chat overrides stored in conversation settings

### Per-Chat MCP Overrides

Each conversation can enable or disable specific MCP servers independently of the global configuration. The override is stored in the conversation's settings and checked before falling back to the global MCP config.

### Transport

MCP connections support three transports:
- **WebSocket**
- **Streamable HTTP** (SSE over HTTP)
- **SSE** (Server-Sent Events)

A CORS proxy is available for development: `--ui-mcp-proxy` (experimental, not for untrusted environments).

### MCP Service

The `MCPService` handles protocol operations: connection management, initialization (exchange capabilities and server info), tool listing, resource reading, and prompt execution.

---

## 6. Draft-MTP (Multi-Token Prediction) Fine-Tuning

llama.cpp supports speculative decoding via a draft model, and can work with MTP fine-tuned models.

### Basic Usage

```bash
llama-server -m target.gguf -md draft.gguf
llama-cli -m target.gguf -md draft.gguf
```

The draft model should be a smaller variant of the target model (e.g., 1B draft for a 7B target).

### MTP-Specific Context Types

When using an MTP fine-tuned model, create the context with:

```c
llama_context_params params = llama_context_default_params();
params.ctx_type = LLAMA_CONTEXT_TYPE_MTP;  // MTP-specific context type
```

Use `llama_init_from_model()` (not the deprecated `llama_new_context_with_model()`).

### Running MTP Models with Built-in Heads

For models with built-in MTP heads (downloaded from HuggingFace), use the `--spec-type mtp` flag:

```bash
llama-server -hf ggml-org/mtp-model-GGUF \
  --spec-type mtp \
  -c 8192 \
  --draft-n-max 3 \
  --draft-n-min 2
```

The MTP head is discovered and loaded automatically from the HuggingFace repo (searches for `mtp-` file in repo).

### Draft KV Cache Configuration

```bash
--spec-draft-type-k f16      # K cache type for draft (default: f16)
--spec-draft-type-v f16      # V cache type for draft (default: f16)
```

Available types: `f32`, `f16`, `bf16`, `q8_0`, `q4_0`, `q4_1`, `iq4_nl`, `q5_0`, `q5_1`.

### Speculative Decoding Parameters

From `common/common.h`, the defaults are:
- `draft.n_max = 3` — maximum number of tokens to draft during speculative decoding
- `draft.n_min = 0` — minimum number of draft tokens to use for speculative decoding
- `draft.p_split = 0.1` — speculative decoding split probability
- `draft.p_min = 0.0` — minimum speculative decoding probability (greedy)

```bash
--draft-n-max 3        # maximum tokens to draft
--draft-n-min 2        # minimum tokens to draft
--draft-p-split 0.1    # split probability
--draft-p-min 0.0      # greedy threshold
```

### Speculative Types

The following speculative decoding types are available:

| Type | Name | Description |
|------|------|-------------|
| `none` | `COMMON_SPECULATIVE_TYPE_NONE` | no speculative decoding |
| `draft-simple` | `COMMON_SPECULATIVE_TYPE_DRAFT_SIMPLE` | standalone draft model |
| `draft-eagle3` | `COMMON_SPECULATIVE_TYPE_DRAFT_EAGLE3` | Eagle3 speculative decoding |
| `draft-mtp` | `COMMON_SPECULATIVE_TYPE_DRAFT_MTP` | Multi-token prediction |
| `ngram-simple` | `COMMON_SPECULATIVE_TYPE_NGRAM_SIMPLE` | simple self-speculative (n-grams) |
| `ngram-map-k` | `COMMON_SPECULATIVE_TYPE_NGRAM_MAP_K` | n-gram keys only |
| `ngram-map-k4v` | `COMMON_SPECULATIVE_TYPE_NGRAM_MAP_K4V` | n-gram keys + 4 m-gram values |
| `ngram-mod` | `COMMON_SPECULATIVE_TYPE_NGRAM_MOD` | modified n-gram speculative |
| `ngram-cache` | `COMMON_SPECULATIVE_TYPE_NGRAM_CACHE` | 3-level n-gram cache |

### MTP Fine-Tuning Workflow

1. **Train** the draft model on the same data as the target, with MTP loss
2. **Quantize** the draft model to GGUF (typically a lower quant like Q4_K_M)
3. **Run** with both models loaded

The draft model generates N tokens speculatively; the target verifies them in parallel. Accepted tokens are kept; rejected tokens trigger re-generation from the last accepted point.

### Performance

Speculative decoding typically provides 2-4x speedup on normal workloads, depending on:
- Draft model quality (acceptance rate)
- Batch size and context length
- Hardware (GPU memory bandwidth often becomes the bottleneck)

---

## 7. GGUF Model Format

### Model File Types

llama.cpp supports these quantization formats (enum `llama_ftype` in `include/llama.h`):

| Type | Code | Description |
|------|------|-------------|
| F16 | 0 | Full float16 |
| Q4_0 | 2 | 4-bit quantization |
| Q4_1 | 3 | 4-bit with mixed precision |
| Q8_0 | 7 | 8-bit quantization |
| Q5_0 | 8 | 5-bit quantization |
| Q5_1 | 9 | 5-bit with mixed precision |
| Q2_K | 10 | K-family 2-bit |
| Q3_K_S | 11 | K-family 3-bit small |
| Q3_K_M | 12 | K-family 3-bit medium |
| Q3_K_L | 13 | K-family 3-bit large |
| Q4_K_S | 14 | K-family 4-bit small |
| Q4_K_M | 15 | K-family 4-bit medium |
| Q5_K_S | 16 | K-family 5-bit small |
| Q5_K_M | 17 | K-family 5-bit medium |
| Q6_K | 18 | K-family 6-bit |
| IQ2_XXS | 19 | 2-bit ultra-small |
| IQ2_XS | 20 | 2-bit extra-small |
| Q2_K_S | 21 | K-family 2-bit small |
| IQ3_XS | 22 | 3-bit extra-small |
| IQ3_XXS | 23 | 3-bit ultra-small |
| IQ1_S | 24 | 1.5-bit small |
| IQ4_NL | 25 | 4-bit new-line |
| IQ3_S | 26 | 3-bit small |
| IQ3_M | 27 | 3-bit medium |
| IQ2_S | 28 | 2-bit small |
| IQ2_M | 29 | 2-bit medium |
| IQ4_XS | 30 | 4-bit extra-small |
| IQ1_M | 31 | 1.5-bit medium |
| BF16 | 32 | Bfloat16 |
| TQ1_0 | 36 | Transquantized 1-bit |
| TQ2_0 | 37 | Transquantized 2-bit |
| MXP4_MOE | 38 | Mixed-precision MoE |
| NVFP4 | 39 | NVIDIA FP4 |
| Q1_0 | 40 | 1-bit quantization |
| GUESSED | 1024 | Not specified in file |

### GGUF Metadata Access

```c
int32_t llama_model_meta_count(const struct llama_model *model);
int32_t llama_model_meta_key_by_index(const struct llama_model *model, int32_t i, char *buf, size_t buf_size);
int32_t llama_model_meta_val_str(const struct llama_model *model, const char *key, char *buf, size_t buf_size);
```

### Model Properties

```c
llama_model_n_ctx_train(model);   // Training context size
llama_model_n_embd(model);        // Embedding dimension
llama_model_n_layer(model);       // Number of layers
llama_model_n_head(model);        // Number of attention heads
llama_model_n_head_kv(model);     // Number of KV heads
llama_model_n_params(model);      // Total parameters (bytes)
llama_model_size(model);          // Model weight size (bytes)
llama_model_desc(model, buf, size); // Human-readable description
```

### Model Type Detection

```c
llama_model_has_encoder(model);    // Encoder-decoder model
llama_model_has_decoder(model);    // Decoder-only model
llama_model_is_recurrent(model);   // RNN-style (Mamba, RWKV)
llama_model_is_hybrid(model);      // Hybrid (Jamba, Granite)
llama_model_is_diffusion(model);   // Diffusion model
```

---

## 8. Grammars (Constrained Decoding)

### GBNF Grammars (Built-in)

llama.cpp includes a GBNF grammar engine. Use with `--grammar` or `--grammar-file`:

```bash
llama-cli -m model.gguf --grammar-file grammars/json.gbnf -p 'Request: '
```

Sample grammars in `grammars/` directory. For complex JSON, use [Grammar Editor](https://grammar.intrinsiclabs.ai/).

### LLGuidance (Rust-based, Recommended)

LLGuidance is a high-performance constrained decoding backend using Lark syntax with JSON Schema support.

**Building**:
```bash
cmake -B build -DLLAMA_LLGUIDANCE=ON
cmake --build build --config Release
```

Requires Rust compiler (`cargo`).

**Usage**:
- Grammars starting with `%llguidance` are passed to LLGuidance
- JSON Schema requests (e.g., `-j` flag) use LLGuidance
- GBNF can be converted with `gbnf_to_lark.py`

**Performance**: Token mask computation averages 50μs per token for llama3 tokenizer (p99: 0.5ms, p100: 20ms).

**JSON Schema Support**: Full JSON Schema compliance with `additionalProperties` defaulting to `true`. Unsupported schemas produce errors.

---

## 9. Multimodal Support

llama.cpp supports multimodal input via `libmtmd`:

```bash
# Vision
llama-server -hf ggml-org/gemma-3-4b-it-GGUF

# Audio
llama-server -hf ggml-org/ultravox-v0_5-llama-3_2-1b-GGUF

# Local files
llama-server -m model.gguf --mmproj mmproj.gguf

# Without GPU offload
llama-server --no-mmproj-offload
```

The multimodal marker string is exposed via `mtmd_default_marker()`. Check `GET /models` for `multimodal` capability before sending multimodal requests.

---

## 10. Embeddings and Reranking

### Embedding Models

```bash
llama-server -m model.gguf --embedding --pooling cls
```

Pooling types: `none`, `mean`, `cls`, `last`, `rank` (for reranking).

Non-OAI endpoint `/embeddings` supports all poolings, including `none` for per-token embeddings.

### Reranking Models

```bash
llama-server -m model.gguf --reranking
```

---

## 11. Build System

### CMake Options

Key options for `cmake -B build` (from `CMakeLists.txt`):

| Option | Default | Purpose |
|--------|---------|---------|
| `LLAMA_ALL_WARNINGS` | ON | Enable all compiler warnings |
| `LLAMA_FATAL_WARNINGS` | OFF | Enable -Werror flag |
| `LLAMA_BUILD_TESTS` | ON | Build tests |
| `LLAMA_BUILD_TOOLS` | ON | Build tools |
| `LLAMA_BUILD_EXAMPLES` | ON | Build examples |
| `LLAMA_BUILD_SERVER` | ON | Build server |
| `LLAMA_BUILD_APP` | ON | Build unified binary |
| `LLAMA_BUILD_UI` | ON | Build embedded Web UI |
| `LLAMA_USE_PREBUILT_UI` | ON | Use prebuilt UI from HF Bucket |
| `LLAMA_OPENSSL` | ON | Use OpenSSL (HTTPS support) |
| `LLAMA_LLGUIDANCE` | OFF | Include LLGuidance for structured output |
| `LLAMA_USE_SYSTEM_GGML` | OFF | Use system libggml |
| `GGML_LLAMAFILE_DEFAULT` | ON | Use llamafile |
| `GGML_CUDA_GRAPHS` | ON | CUDA graphs |

### NVIDIA CUDA Build (V100 Example)

```bash
cmake -B build \
  -DGGML_CUDA=ON \
  -DCMAKE_CUDA_ARCHITECTURES=70 \
  -DGGML_NATIVE=OFF \
  -DGGML_CUDA_GRAPHS=ON \
  -DLLAMA_OPENSSL=ON \
  -DBUILD_SHARED_LIBS=ON
cmake --build build --config Release -j $(nproc)
```

Key points:
- `GGML_CUDA=ON` — enables CUDA backend
- `CMAKE_CUDA_ARCHITECTURES=70` — V100 is Pascal (compute 7.0); avoids JIT compilation overhead of `-arch=native`
- `GGML_NATIVE=OFF` — non-native build; avoids JIT compilation but produces larger binary
- `GGML_CUDA_GRAPHS=ON` — CUDA graphs enabled (default)
- `LLAMA_OPENSSL=ON` — enables HTTPS/TLS support for the server
- `BUILD_SHARED_LIBS=ON` — default for most platforms (not Windows)
- `-j $(nproc)` — parallel compilation

### Other GPU Backends

| Backend | CMake Flag |
|---------|------------|
| CUDA | `-DGGML_CUDA=ON` |
| Vulkan | `-DGGML_VULKAN=ON` |
| Metal (macOS) | `-DGGML_METAL=ON` (default on macOS) |
| SYCL (Intel GPU) | `-DGGML_SYCL=ON` |
| HIP (AMD GPU) | `-DGGML_HIP=ON -DGPU_TARGETS=gfx1030` |
| MUSA (Moore Threads) | `-DGGML_MUSA=ON` |
| CANN (Ascend NPU) | `-DGGML_CANN=ON` |
| OpenCL | `-DGGML_OPENCL=ON` |
| WebGPU | `-DGGML_WEBGPU=ON` |

---

## 12. Multi-Model Router

Start the server without a model to enable routing:

```bash
llama-server
```

Models are loaded on-demand. Control with:
```bash
llama-server --no-models-autoload   # Disable auto-load
llama-server --models-dir ./models  # Custom model directory
llama-server --models-preset ./presets.ini  # INI preset file
```

Route requests by model name in the request body (`model` field) or query parameter (`?model=...`).

### Preset Files (INI Format)

```ini
version = 1

[global]
ctx-size = 8192

[my-model]
model = /path/to/model.gguf
n-gpu-layers = 33
temp = 0.7
chat-template = chatml
load-on-startup = true
stop-timeout = 30

[gpt-oss-20b]
hf = ggml-org/gpt-oss-20b-GGUF
batch-size = 2048
top-p = 1.0
temp = 1.0
chat-template-kwargs = {"reasoning_effort": "high"}
```

---

## 13. LoRA Adapters

```bash
llama-server --lora adapter1.gguf:0.5,adapter2.gguf:1.0
```

Per-request LoRA:
```json
{
  "messages": [...],
  "lora": [{"id": 0, "scale": 0.5}, {"id": 1, "scale": 1.1}]
}
```

List loaded adapters: `GET /lora-adapters`. Set global scale: `POST /lora-adapters`.

---

## 14. Control Vectors

```bash
--control-vector vector.gguf
--control-vector-scaled vector.gguf:1.5
--control-vector-layer-range 1 10
```

---

## 15. Environment Variables

Common environment variables (from `common/arg.cpp`):

| Variable | Purpose |
|----------|---------|
| `LLAMA_ARG_CTX_SIZE` | Context size |
| `LLAMA_ARG_N_PREDICT` | Number of tokens to predict |
| `LLAMA_ARG_HF_REPO` | HuggingFace model repo |
| `LLAMA_ARG_HF_REPO_V` | Vocoder model repo |
| `HF_TOKEN` | HuggingFace access token |
| `LLAMA_OFFLINE` | Offline mode |
| `LLAMA_LOG_VERBOSITY` | Logging verbosity level |
| `LLAMA_LOG_FILE` | Log file path |
| `LLAMA_ARG_CACHE_RAM` | Cache RAM setting |

---

## 16. Docker

```bash
# CPU-only
docker run -p 8080:8080 -v ./models:/models ghcr.io/ggml-org/llama.cpp:server

# CUDA
docker run -p 8080:8080 -v ./models:/models --gpus all ghcr.io/ggml-org/llama.cpp:server-cuda
```

---

## 17. Error Formats

llama-server returns errors in OpenAI format:
```json
{"error": {"code": 401, "message": "Invalid API Key", "type": "authentication_error"}}
```

Custom llama.cpp errors:
- `501 not_supported_error` — endpoint disabled
- `400 invalid_request_error` — grammar parse failure
- `503 unavailable_error` — model loading

---

## 18. Performance Monitoring

### Context Performance

```c
llama_perf_context_data perf = llama_perf_context(ctx);
// perf.t_load_ms, perf.t_p_eval_ms, perf.t_eval_ms
// perf.n_p_eval, perf.n_eval, perf.n_reused
llama_perf_context_print(ctx);
llama_perf_context_reset(ctx);
```

### Sampling Performance

```c
llama_perf_sampler_data perf = llama_perf_sampler(chain);
// perf.t_sample_ms, perf.n_sample
```

### Prometheus Metrics

Enable with `--metrics`. Available metrics:
- `llamacpp:prompt_tokens_total`, `llamacpp:prompt_seconds_total`
- `llamacpp:tokens_predicted_total`, `llamacpp:tokens_predicted_seconds_total`
- `llamacpp:requests_processing`, `llamacpp:requests_deferred`
- `llamacpp:n_tokens_max`, `llamacpp:n_decode_total`

---

## 19. Deprecation Map

Old → New:
- `llama_new_context_with_model()` → `llama_init_from_model()`
- `llama_get_state_size()` → `llama_state_get_size()`
- `llama_copy_state_data()` → `llama_state_get_data()`
- `llama_set_state_data()` → `llama_state_set_data()`
- `llama_load_session_file()` → `llama_state_load_file()`
- `llama_save_session_file()` → `llama_state_save_file()`
- `llama_token_*()` → `llama_vocab_*()` (token functions moved to vocab)
- `llama_token_bos()` → `llama_vocab_bos()`
- `llama_token_is_eog()` → `llama_vocab_is_eog()`
- `llama_free_model()` → `llama_model_free()`

---

## 20. XCFramework

Precompiled framework for iOS/visionOS/tvOS/macOS. Download from releases:

```swift
import PackageDescription
let package = Package(
    name: "MyLlamaPackage",
    targets: [.binaryTarget(name: "LlamaFramework", url: "...", checksum: "...")]
)
```

---

## 21. Tokenization Details

### Token Attributes

```c
enum llama_token_attr {
    LLAMA_TOKEN_ATTR_UNKNOWN      = 1 << 0,
    LLAMA_TOKEN_ATTR_UNUSED       = 1 << 1,
    LLAMA_TOKEN_ATTR_NORMAL       = 1 << 2,
    LLAMA_TOKEN_ATTR_CONTROL      = 1 << 3,
    LLAMA_TOKEN_ATTR_USER_DEFINED = 1 << 4,
    LLAMA_TOKEN_ATTR_BYTE         = 1 << 5,
    LLAMA_TOKEN_ATTR_NORMALIZED   = 1 << 6,
    LLAMA_TOKEN_ATTR_LSTRIP       = 1 << 7,
    LLAMA_TOKEN_ATTR_RSTRIP       = 1 << 8,
    LLAMA_TOKEN_ATTR_SINGLE_WORD  = 1 << 9,
};
```

### Token Pieces

```c
llama_token_to_piece(vocab, token, buf, length, lstrip, special);
```

The `special` parameter controls whether control tokens (like `<|start_header_id|>`) are rendered in their symbolic form or as the decoded text.

---

## 22. Vocab Types

From `include/llama.h`:

```c
enum llama_vocab_type {
    LLAMA_VOCAB_TYPE_NONE   = 0,  // Models without vocab
    LLAMA_VOCAB_TYPE_SPM    = 1,  // LLaMA tokenizer (byte-level BPE + byte fallback)
    LLAMA_VOCAB_TYPE_BPE    = 2,  // GPT-2 tokenizer
    LLAMA_VOCAB_TYPE_WPM    = 3,  // BERT tokenizer (WordPiece)
    LLAMA_VOCAB_TYPE_UGM    = 4,  // T5 tokenizer (Unigram)
    LLAMA_VOCAB_TYPE_RWKV   = 5,  // RWKV tokenizer (greedy)
    LLAMA_VOCAB_TYPE_PLAMO2 = 6,  // PLaMo-2 tokenizer (Aho-Corasick + DP)
};
```

### RoPE Types

```c
enum llama_rope_type {
    LLAMA_ROPE_TYPE_NONE   = -1,
    LLAMA_ROPE_TYPE_NORM   = 0,
    LLAMA_ROPE_TYPE_NEOX   = 1,
    LLAMA_ROPE_TYPE_MROPE  = 2,
    LLAMA_ROPE_TYPE_IMROPE = 3,
    LLAMA_ROPE_TYPE_VISION = 4,
};
```

### RoPE Scaling Types

```c
enum llama_rope_scaling_type {
    LLAMA_ROPE_SCALING_TYPE_UNSPECIFIED = -1,
    LLAMA_ROPE_SCALING_TYPE_NONE        = 0,
    LLAMA_ROPE_SCALING_TYPE_LINEAR      = 1,
    LLAMA_ROPE_SCALING_TYPE_YARN        = 2,
    LLAMA_ROPE_SCALING_TYPE_LONGROPE    = 3,
};
```

---

## 23. Memory Operations

llama.cpp provides a `llama_memory_t` abstraction for managing the KV cache:

```c
// Clear memory contents (with or without data)
llama_memory_clear(mem, data);

// Remove tokens from a sequence
llama_memory_seq_rm(mem, seq_id, p0, p1);

// Copy tokens between sequences
llama_memory_seq_cp(mem, seq_id_src, seq_id_dst, p0, p1);

// Keep only tokens of a specific sequence
llama_memory_seq_keep(mem, seq_id);

// Shift positions by delta
llama_memory_seq_add(mem, seq_id, p0, p1, delta);

// Divide positions by factor d
llama_memory_seq_div(mem, seq_id, p0, p1, d);

// Get min/max position in sequence
llama_pos llama_memory_seq_pos_min(mem, seq_id);
llama_pos llama_memory_seq_pos_max(mem, seq_id);
```

---

*This guide covers the major features and APIs as of the current master branch. For the latest changes, see the [libllama API changelog](https://github.com/ggml-org/llama.cpp/issues/9289) and [server API changelog](https://github.com/ggml-org/llama.cpp/issues/9291).*
