# Space Compiler

A .NET 8 API for compiling documents into structured Abstract Syntax Trees (AST) with semantic analysis and self-attention computation.

Comprehensive compiler of anything that contains data or semantics. Feature potential replacement of transformer (GPT) architecture.

## Overview

Space Compiler is a new methodology for document compilation, extracting the file parsing API from the [space_db_public](https://github.com/xlab2016/space_db_public) project.

### Vision: Space Compiler Model

The **Space Compiler** represents a novel approach to language modeling as an alternative to traditional Large Language Models (LLMs). Key features:

- **Infinite Context Length**: Unlike transformer-based models with fixed context windows, Space Compiler builds an ontology from input that can scale indefinitely
- **Self-Attention Mechanism**: Computes relevance scores between all content blocks, identifying important relationships and patterns
- **Neural Reasoning Foundation**: The attention matrix provides a foundation for neural reasoning over the compiled ontology
- **Space DB Integration**: Uses [space_db](https://github.com/xlab2016/space_db_public) as persistent memory for the model

### Compilation Pipeline

The compiler provides a four-stage pipeline:

1. **Tokenization** - Breaking text into hierarchical fragments (files, sections, paragraphs, sentences, phrases, words)
2. **Parsing** - Building AST trees from tokens, creating structured blocks
3. **Analysis** - Performing semantic analysis using heuristics and statistical methods
4. **Self-Attention** - Computing attention matrix with relevance scores and coherence probabilities between all blocks

## Architecture

### Services

- **TokenizerService**: Splits text into fragments
  - Supports text and JSON content types
  - Handles paragraph merging and splitting
  - Preserves hierarchical structure for JSON

- **ParserService**: Builds AST from tokens
  - Groups fragments into blocks
  - Maintains order and relationships
  - Configurable block size limits

- **AnalyzerService**: Performs semantic analysis
  - Word frequency analysis
  - Keyword extraction
  - Readability scoring
  - Content categorization
  - Statistical pattern detection

- **AttentionService**: Computes self-attention between blocks
  - TF-IDF vectorization of content blocks
  - Cosine similarity calculation for relevance
  - Softmax normalization for attention scores
  - Coherence probability computation
  - Adjacent block relationship boosting

- **SpaceProjParser**: Parses .spaceproj files
  - Based on [links-notation](https://github.com/link-foundation/links-notation)
  - Builds project graph structures
  - Supports hierarchical relationships

- **CompilationService**: Orchestrates the compilation pipeline
  - Single file compilation
  - Multi-file compilation
  - Project compilation from ZIP archives
  - Automatic self-attention computation

## API Endpoints

### `/api/v1/compiler/compile/file`
Compile a single file.

**Request Body:**
```json
{
  "content": "File content here...",
  "fileName": "document.txt",
  "contentType": "text"
}
```

### `/api/v1/compiler/compile/files`
Compile multiple files as a unified tree.

**Request Body:**
```json
{
  "files": {
    "file1.txt": "Content 1...",
    "file2.txt": "Content 2..."
  }
}
```

### `/api/v1/compiler/compile/project/zip`
Compile a project from a ZIP file with `.spaceproj` structure.

**Form Data:**
- `file`: ZIP file containing project files and a `.spaceproj` file

## .spaceproj Format

The `.spaceproj` file uses links-notation to define project structure:

```
Собаки: Files/File1.doc
Кошки: Files/File2.doc
Животные: File3.doc
Животные: (Собаки Кошки)
```

This creates a graph structure:
```
Животные
├── Собаки (Files/File1.doc)
└── Кошки (Files/File2.doc)
```

## Running the Project

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run API
cd src/SpaceCompiler
dotnet run
```

The API will be available at `https://localhost:5001` with Swagger UI at `/swagger`.

## Self-Attention Matrix

The compilation result includes an `AttentionMatrix` with:

- **Attention Scores**: N×N matrix where entry [i,j] represents the attention score from block i to block j
  - Computed using TF-IDF + Cosine Similarity
  - Normalized with softmax for each row (scores sum to 1.0)
  - Higher scores indicate stronger semantic relevance

- **Coherence Probabilities**: N×N matrix representing flow coherence between blocks
  - Boosted for adjacent blocks in the same resource
  - Distance-penalized for non-adjacent blocks
  - Normalized to sum to 1.0 per row

- **Block References**: Metadata for each block including resource ID, order, type, and content preview

### Example Response

```json
{
  "resources": [...],
  "attentionMatrix": {
    "scores": [[1.0, 0.3, 0.1], [0.3, 1.0, 0.2], [0.1, 0.2, 1.0]],
    "coherenceProbabilities": [[0.5, 0.4, 0.1], [0.3, 0.5, 0.2], [0.2, 0.3, 0.5]],
    "blocks": [
      {
        "resourceId": "file.txt",
        "blockOrder": 0,
        "blockType": "block",
        "contentPreview": "First block content...",
        "contentLength": 150
      }
    ],
    "metadata": {
      "computed_at": "2025-01-13T12:00:00Z",
      "block_count": 3,
      "resource_count": 1,
      "vocabulary_size": 45,
      "algorithm": "TF-IDF + Cosine Similarity + Softmax"
    }
  }
}
```

## Testing

The project includes comprehensive unit tests for all services:
- TokenizerService tests
- ParserService tests
- SpaceProjParser tests
- AttentionService tests

Run tests with:
```bash
dotnet test
```

## Technologies

- .NET 8
- ASP.NET Core Web API
- Swashbuckle (Swagger/OpenAPI)
- xUnit
- FluentAssertions
- Moq

## License

This project is part of the Space ecosystem.