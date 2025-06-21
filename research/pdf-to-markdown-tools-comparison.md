# PDF-to-Markdown Conversion Tools: Comprehensive Comparison

> **Purpose**: Evaluate tools for converting academic research papers (arXiv PDFs) to high-quality Markdown for our agent research knowledge base.
>
> **Last Updated**: June 2025 | Based on deep online research including GitHub repos, OmniDocBench (CVPR 2025), olmOCR-Bench, and Reddit community discussions.

---

## Table of Contents

1. [Executive Summary & Recommendation](#executive-summary--recommendation)
2. [Benchmark Results](#benchmark-results)
3. [Tier 1: Best for Academic Papers](#tier-1-best-for-academic-papers)
4. [Tier 2: Strong Alternatives](#tier-2-strong-alternatives)
5. [Tier 3: Not Ideal for This Use Case](#tier-3-not-ideal-for-this-use-case)
6. [Emerging / Community Mentions](#emerging--community-mentions)
7. [Quick Comparison Matrix](#quick-comparison-matrix)
8. [Community Insights (Reddit)](#community-insights-reddit)
9. [Decision Factors for Our Use Case](#decision-factors-for-our-use-case)

---

## Executive Summary & Recommendation

After extensive research across GitHub repositories, academic benchmarks, and community discussions, the **top 3 tools for converting arXiv research papers to Markdown** are:

| Rank | Tool | Why |
|------|------|-----|
| **#1** | **Marker** | Best balance of accuracy (96.67% on scientific papers per own benchmarks), speed (~25 pages/sec on H100), ease of use (`pip install marker-pdf`), hybrid LLM mode for math/tables, active development. Most recommended by Reddit users for academic papers. |
| **#2** | **Docling** | MIT license (best for open use), IBM-backed, LF AI & Data Foundation project, excellent integrations (LangChain, LlamaIndex), VLM support, very active (190 contributors). Slightly lower accuracy than Marker on benchmarks. |
| **#3** | **MinerU** | Highest OmniDocBench scores (90.67 overall with 1.2B model), 109-language OCR, hybrid backend, but "annoying to get going" per community. AGPL-3.0 license. |

**For our specific use case** (English arXiv papers, digital PDFs, math-heavy, local execution): **Marker** is the recommended choice, with **Docling** as the runner-up for its MIT license and ecosystem integrations.

---

## Benchmark Results

### OmniDocBench v1.5 (CVPR 2025)

The most comprehensive academic benchmark for document parsing. 1,355 PDF pages across 9 document types. Overall = ((1 - Text Edit Distance) × 100 + Table TEDS + Formula CDM) / 3.

#### Pipeline Tools (most relevant to our use case — local, no large GPU required)

| Tool | Overall | Text Edit Dist↓ | Text Score | Table TEDS | Formula CDM | Formula Edit Dist↓ |
|------|---------|-----------------|------------|------------|-------------|-------------------|
| PP-StructureV3 | **86.73** | 0.073 | 85.79 | 81.68 | 89.48 | 0.073 |
| MinerU2-pipeline | 75.51 | 0.209 | 76.55 | 70.90 | 79.11 | 0.225 |
| Marker 1.8.2 | 71.30 | 0.206 | 76.66 | 57.88 | 71.17 | 0.250 |

> **Note**: These are pipeline-tool scores without VLM augmentation. Marker's `--use_llm` mode and MinerU's VLM backend score significantly higher (see below).

#### Specialized VLMs (require GPU, higher accuracy)

| Tool | Params | Overall | Text Edit↓ | Text | Table | Formula | Formula Edit↓ |
|------|--------|---------|-----------|------|-------|---------|---------------|
| PaddleOCR-VL | 0.9B | **92.86** | 0.035 | 91.22 | 90.89 | 94.76 | 0.043 |
| MinerU2.5 | 1.2B | **90.67** | 0.047 | 88.46 | 88.22 | 92.38 | 0.044 |
| Deepseek-OCR | 3B | 87.01 | 0.073 | 83.37 | 84.97 | 88.80 | 0.086 |
| MinerU2-VLM | 0.9B | 85.56 | 0.078 | 80.95 | 83.54 | 87.66 | 0.086 |
| olmOCR | 7B | 81.79 | 0.096 | 86.04 | 68.92 | 74.77 | 0.121 |
| Dolphin-1.5 | 0.3B | 83.21 | 0.092 | 80.78 | 78.06 | 84.10 | 0.080 |
| Mistral OCR | - | 78.83 | 0.164 | 82.84 | 70.03 | 78.04 | 0.144 |
| Nougat | - | *Not in v1.5 leaderboard* | | | | | |

#### General VLMs (large models, API or large GPU)

| Model | Params | Overall |
|-------|--------|---------|
| Qwen3-VL | 235B | 89.15 |
| Gemini-2.5 Pro | - | 88.03 |
| Qwen2.5-VL | 72B | 87.02 |
| GPT-4o | - | 75.02 |

### olmOCR-Bench (Allen AI)

7,000+ test cases across 1,400 documents. Different benchmark methodology.

| Tool | Score |
|------|-------|
| Chandra OCR 0.1.0 | **83.1** |
| Infinity-Parser 7B | 82.5 |
| olmOCR v0.4.0 | 82.4 |
| PaddleOCR-VL | 80.0 |
| Marker 1.10.1 | **76.1** |
| DeepSeek-OCR | 75.7 |
| MinerU 2.5.4 | 75.2 |
| Mistral OCR API | 72.0 |
| Nanonets-OCR2-3B | 69.5 |

### Marker's Own Benchmarks (Scientific Papers)

Per Marker's README, on scientific papers specifically: **96.67% accuracy**, beating LlamaParse, Mathpix, and Docling. Throughput: ~25 pages/sec on H100, ~6 pages/sec on A10G.

---

## Tier 1: Best for Academic Papers

### 1. Marker ⭐ (Recommended)

| Attribute | Details |
|-----------|---------|
| **GitHub** | [datalab-to/marker](https://github.com/datalab-to/marker) |
| **Stars** | 32.2k ⭐ |
| **License** | GPL-3.0 (code), Modified AI Pubs Open Rail-M (model weights) |
| **Install** | `pip install marker-pdf` |
| **Python** | 3.10+ |
| **Last Commit** | ~3 days ago (very active) |
| **Version** | v1.10.2 (Jan 2025) |
| **Contributors** | 29 |

**Key Features:**
- Supports PDF, images, PPTX, DOCX, XLSX, HTML, EPUB
- **Tables, forms, equations, inline math, links, references, code blocks**
- Extract and save images
- GPU, CPU, and MPS support
- **Hybrid LLM mode** (`--use_llm`): Uses Gemini/Ollama/Claude/OpenAI to improve quality — merges tables across pages, improves inline math, formats tables better

**How It Works:**
Pipeline of deep learning models: text extraction/OCR (Surya) → layout detection/reading order (Surya) → clean/format (heuristics + Texify + Surya) → optional LLM → combine/postprocess

**Usage:**
```bash
# CLI
marker_single /path/to/paper.pdf
marker_single paper.pdf --use_llm --output_format markdown

# Python API
from marker.converters.pdf import PdfConverter
converter = PdfConverter()
rendered = converter("paper.pdf")
text, _, images = text_from_rendered(rendered)
```

**Strengths:**
- Best-in-class for scientific papers (96.67% accuracy)
- Fast throughput
- Simple installation (`pip install marker-pdf`)
- Hybrid LLM mode significantly improves math/table handling
- Very active development
- Outputs: Markdown, JSON, HTML, chunks

**Weaknesses:**
- GPL-3.0 license (restrictive for commercial use)
- LLM mode requires API key (adds cost)
- Base mode (without LLM) scores lower on OmniDocBench

---

### 2. Docling

| Attribute | Details |
|-----------|---------|
| **GitHub** | [docling-project/docling](https://github.com/docling-project/docling) |
| **Stars** | 55.2k ⭐ |
| **License** | **MIT** (most permissive) |
| **Install** | `pip install docling` |
| **Python** | 3.10+ |
| **Last Commit** | ~2 days ago |
| **Version** | v2.77.0 |
| **Contributors** | 190 |

**Key Features:**
- Multiple formats: PDF, DOCX, PPTX, XLSX, HTML, WAV, MP3, images, LaTeX, XBRL
- Advanced PDF understanding: layout, reading order, tables, code, formulas, image classification
- Unified `DoclingDocument` format
- Export to Markdown, HTML, DocTags, JSON
- **VLM support**: GraniteDocling (IBM's 258M model), other VLMs via CLI
- **Integrations**: LangChain, LlamaIndex, Crew AI, Haystack, **MCP server**

**Usage:**
```bash
# CLI — can convert directly from arXiv URLs!
docling https://arxiv.org/pdf/2206.01062

# Python API
from docling.document_converter import DocumentConverter
result = DocumentConverter().convert("paper.pdf")
markdown = result.document.export_to_markdown()
```

**Strengths:**
- **MIT license** — most permissive
- IBM-backed, LF AI & Data Foundation project
- Largest contributor community (190)
- Excellent ecosystem integrations
- Can convert directly from URLs
- VLM support for enhanced accuracy

**Weaknesses:**
- Slightly lower accuracy than Marker in Marker's benchmarks
- Lower on OmniDocBench pipeline scores than PP-StructureV3

---

### 3. MinerU

| Attribute | Details |
|-----------|---------|
| **GitHub** | [opendatalab/MinerU](https://github.com/opendatalab/MinerU) |
| **Stars** | 55.7k ⭐ |
| **License** | AGPL-3.0 |
| **Install** | `uv pip install -U "mineru[all]"` |
| **Python** | 3.10-3.13 |
| **Last Commit** | Active |
| **Version** | v2.7.6 |
| **Contributors** | 72 |

**Key Features:**
- Headers/footers removal, multi-column/complex layouts
- Structure preservation, image/table/footnote extraction
- **Auto LaTeX formula conversion**, auto HTML table conversion
- OCR for **109 languages**
- GPU, CPU, NPU, MPS support
- Three backends: `pipeline` (CPU OK, 82+ accuracy), `vlm` (GPU needed, 90+ accuracy), `hybrid` (combines both)

**Usage:**
```bash
# CLI
mineru -p paper.pdf -o output/

# Python API
from mineru import MinerU
mu = MinerU()
result = mu.convert("paper.pdf")
```

**Strengths:**
- **Highest OmniDocBench scores** (90.67 with VLM backend)
- Born from InternLM pretraining pipeline — battle-tested on millions of papers
- 109-language OCR support
- Very flexible backend options

**Weaknesses:**
- AGPL-3.0 license
- "Annoying to get going" per Reddit community
- VLM backend requires 6-10GB+ VRAM
- More complex installation than Marker/Docling

---

## Tier 2: Strong Alternatives

### 4. olmOCR (Allen AI)

| Attribute | Details |
|-----------|---------|
| **GitHub** | [allenai/olmocr](https://github.com/allenai/olmocr) |
| **Stars** | 17k ⭐ |
| **License** | Apache-2.0 |
| **Install** | `pip install olmocr[gpu]` |
| **Requires** | Recent NVIDIA GPU (12GB+ VRAM), 30GB disk |
| **Version** | v0.4.25 |

**Key Features:**
- Convert PDF, PNG, JPEG to clean Markdown
- Equations, tables, handwriting, complex formatting
- Auto removes headers/footers
- Natural reading order across multi-column layouts
- Based on fine-tuned 7B VLM (Qwen2.5-VL)
- Efficient: <$200 USD per million pages
- Docker support, multi-node/cluster support
- External API provider support (DeepInfra, Parasail, etc.)

**Strengths:**
- **Apache-2.0 license** (very permissive)
- Allen AI backed (non-profit, research-focused)
- Designed for large-scale processing (millions of PDFs)
- olmOCR-Bench score: 82.4 (top tier in its own benchmark)
- RL-trained model (olmOCR v2) with unit test rewards

**Weaknesses:**
- **Requires GPU** (12GB+ VRAM minimum)
- Heavier setup (needs poppler-utils, fonts, conda env)
- OmniDocBench overall: 81.79 (below MinerU, Marker with LLM)
- 7B model is resource-intensive compared to pipeline tools

---

### 5. PyMuPDF4LLM

| Attribute | Details |
|-----------|---------|
| **GitHub** | [pymupdf/pymupdf4llm](https://github.com/pymupdf/pymupdf4llm) |
| **Stars** | 1.4k ⭐ |
| **License** | AGPL-3.0 |
| **Install** | `pip install -U pymupdf4llm` |

**Key Features:**
- Clean structured Markdown output
- Preserves hierarchy (headings, bold, italic, lists, tables, links)
- Multi-column support
- Image extraction
- Page chunking for RAG
- LlamaIndex integration
- All MuPDF file types (PDF, XPS, eBooks)

**Usage:**
```python
import pymupdf4llm
md_text = pymupdf4llm.to_markdown("paper.pdf")
```

**Strengths:**
- **Extremely fast** — no ML models needed for digital PDFs
- **Simplest API** — one line of code
- Lightweight installation
- Great for digital (non-scanned) PDFs
- Good for batch processing

**Weaknesses:**
- No ML/DL-based understanding — relies on PDF structure
- **Weaker on complex equations** (no LaTeX conversion)
- Struggles with scanned PDFs
- No VLM/LLM augmentation option

---

### 6. Nougat (Meta/Facebook Research)

| Attribute | Details |
|-----------|---------|
| **GitHub** | [facebookresearch/nougat](https://github.com/facebookresearch/nougat) |
| **Stars** | 9.9k ⭐ |
| **License** | MIT (code), CC-BY-NC (model weights) |
| **Install** | `pip install nougat-ocr` |

**Key Features:**
- Neural OCR **specifically designed for academic documents**
- Understands LaTeX math and tables natively
- Mathpix Markdown-compatible output
- Built on Donut architecture
- Trained on arXiv and PubMed Central papers

**Usage:**
```bash
nougat path/to/paper.pdf -o output_directory
```

**Strengths:**
- Purpose-built for exactly our use case (academic papers)
- Excellent LaTeX formula rendering
- MIT license (code)

**Weaknesses:**
- **Less actively maintained** (last commit ~1 year ago)
- CC-BY-NC model weights (non-commercial)
- Limited to English/Latin languages
- Slower than pipeline-based alternatives
- Not on OmniDocBench v1.5 leaderboard

---

## Tier 3: Not Ideal for This Use Case

### 7. MarkItDown (Microsoft)

- **Stars**: 90.3k ⭐ | **License**: MIT
- General-purpose lightweight utility for LLM consumption
- **Not specialized** for academic papers — no advanced equation/table handling
- Good for: quick-and-dirty conversion of business documents
- Not suitable for: math-heavy research papers with complex layouts

### 8. Unstructured

- **Stars**: 14.1k ⭐ | **License**: Apache-2.0
- ETL pipeline framework for unstructured data
- Overkill for just PDF conversion — designed for data pipelines
- Complex installation (libmagic, poppler, tesseract + system deps)
- Better suited for: enterprise document processing pipelines

### 9. Jina Reader

- **Stars**: 10.1k ⭐ | **License**: Apache-2.0
- Primarily a **web page reader** (`r.jina.ai/URL`)
- TypeScript-based, not a local PDF converter
- Not suitable for: local academic PDF processing

---

## Emerging / Community Mentions

These tools were mentioned in Reddit discussions but are newer, API-only, or less established:

| Tool | Notes |
|------|-------|
| **PaddleOCR-VL** | Highest OmniDocBench score (92.86). Fast batch processing. Chinese ecosystem. |
| **DeepSeek OCR** | 3B model, OmniDocBench 87.01. Recently released. |
| **Gemini Direct** | "Convert this document to markdown" — community reports it "blows away" dedicated tools. API-only, not local. |
| **MistralOCR** | API-only. "Nearly flawless" per Reddit. OmniDocBench 78.83 (moderate). |
| **Qwen3-VL** | 235B model, OmniDocBench 89.15. Requires massive GPU. |
| **PaperLab.ai** | Mentioned for scientific papers with complex equations. Web service. |
| **PP-StructureV3** | OmniDocBench 86.73 (top pipeline tool). Part of PaddlePaddle ecosystem. |
| **Dolphin** | Lightweight (0.3B), OmniDocBench 83.21. Good for resource-constrained environments. |

---

## Quick Comparison Matrix

| Tool | Stars | License | Install Ease | GPU Required | Math/LaTeX | Tables | Speed | OmniDocBench Overall | Active Dev |
|------|-------|---------|-------------|--------------|------------|--------|-------|---------------------|------------|
| **Marker** | 32.2k | GPL-3.0 | ⭐⭐⭐⭐⭐ | Optional | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Fast | 71.3 (base) / ~85+ (LLM) | ✅ Very |
| **Docling** | 55.2k | **MIT** | ⭐⭐⭐⭐ | Optional | ⭐⭐⭐⭐ | ⭐⭐⭐⭐ | Medium | ~74* | ✅ Very |
| **MinerU** | 55.7k | AGPL-3.0 | ⭐⭐⭐ | Optional | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | Medium | 75.5 (pipe) / 90.7 (VLM) | ✅ Very |
| **olmOCR** | 17k | Apache-2.0 | ⭐⭐ | **Yes** | ⭐⭐⭐⭐ | ⭐⭐⭐ | Medium | 81.8 | ✅ Active |
| **PyMuPDF4LLM** | 1.4k | AGPL-3.0 | ⭐⭐⭐⭐⭐ | No | ⭐⭐ | ⭐⭐⭐ | **Fastest** | N/A | ✅ Active |
| **Nougat** | 9.9k | MIT/CC-BY-NC | ⭐⭐⭐⭐ | Yes (rec.) | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | Slow | N/A | ❌ Stale |
| **MarkItDown** | 90.3k | MIT | ⭐⭐⭐⭐⭐ | No | ⭐ | ⭐⭐ | Fast | N/A | ✅ Active |

*Docling not individually scored in latest OmniDocBench v1.5 end-to-end leaderboard shown above; was evaluated in v1.0.

---

## Community Insights (Reddit)

### r/LocalLLaMA — "Help me pick a PDF to Markdown/JSON converter"
- **"Marker is the only one that worked very well with minimal efforts for me, using gemini-flash"** (+3 upvotes)
- "Just use docling" — simple recommendation
- "I've tried docling, marker, and pymupdf4llm. I convert a lot of business academic literature papers. I've had the most success with **pymupdf4llm**"
- "Use Gemini directly with 'convert this document to markdown' prompt. We tested against docling and it blows it away"
- "Just use gemini 2.5 pro directly! It's better at pdf conversions than most dedicated software"

### r/LocalLLaMA — "Best way to convert coding/math-heavy PDFs"
- **"Marker has been my go-to for tech books lately. Way faster than MinerU and handles code blocks pretty decently"**
- "PaddleOCR works if you spend a lot of time... MistralOCR3 is nearly flawless and fast, but is not local"
- ParseExtract mentioned for math-heavy PDFs

### r/LocalLLaMA — "Best OCR model for converting PDF pages to markdown"
- **"MinerU is best but bit annoying to get going. Dolphin and Marker are next best."** (+4 upvotes)
- "PaddleOCR works extremely fast for its quality. On 2xRTX 3060 it takes about 4 minutes for a 700+ PDF" (+9)
- "Docling" (+4)
- "Look at deepseek ocr" (+5)
- "Marker is good as well" (+1)

---

## Decision Factors for Our Use Case

### Requirements
- **Document type**: arXiv research papers (academic, English)
- **Content**: Math-heavy (LaTeX equations), tables, multi-column layouts
- **PDF type**: Digital (not scanned) — born-digital from LaTeX
- **Scale**: ~31 papers currently, potentially more
- **Execution**: Local (no API dependency preferred)
- **Output**: Clean Markdown with preserved structure

### Why Marker is Recommended

1. **Scientific paper accuracy**: 96.67% on scientific papers (per own benchmarks)
2. **Math handling**: Built-in Texify for LaTeX equations + optional LLM mode for improvement
3. **Ease of use**: `pip install marker-pdf` → `marker_single paper.pdf` — works out of the box
4. **Speed**: Fast even on CPU, blazing on GPU
5. **Flexibility**: Works on CPU (no GPU required), but benefits from GPU
6. **LLM hybrid mode**: Can use Gemini Flash (cheap) to significantly improve equation/table quality
7. **Active development**: Updates every few days, responsive to issues
8. **Community validated**: Most frequently recommended in Reddit discussions for academic papers

### When to Consider Alternatives

| If you need... | Consider... |
|----------------|-------------|
| MIT license | **Docling** |
| Highest possible accuracy (with GPU) | **MinerU** (VLM backend) |
| Fastest possible conversion (no ML) | **PyMuPDF4LLM** |
| Large-scale processing (millions of PDFs) | **olmOCR** |
| Apache-2.0 license + GPU available | **olmOCR** |
| Purpose-built for academic papers (legacy) | **Nougat** (but stale) |

---

## Next Steps

1. **Install chosen tool** and test on a sample arXiv paper
2. **Compare output quality** on a paper with complex equations and tables
3. **Build automation script** to download PDFs from arXiv and batch convert
4. **Re-process existing 31 papers** with proper PDF extraction
5. **Integrate into knowledge base pipeline**
