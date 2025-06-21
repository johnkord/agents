---
name: convert-paper
description: >
  Downloads research papers from arXiv as PDFs and converts them to Markdown
  using Docling. Use when adding new research papers to the knowledge base,
  when the user mentions arXiv papers, or when asked to download or convert
  academic papers. Handles the full pipeline: finding papers, downloading PDFs,
  converting to Markdown, and updating the conversion script.
---

# Convert Research Paper

Add arXiv research papers to the knowledge base by downloading the PDF and converting it to faithful Markdown.

## Directory Structure

```
papers/
├── pdfs/          # Source PDFs downloaded from arXiv
└── docling/       # Faithful Markdown conversions of the PDFs
scripts/
└── convert_papers.py  # Automated download + conversion pipeline
```

## Step-by-Step Procedure

### 1. Find the paper on arXiv

- Search at https://arxiv.org/search/
- Extract the arXiv ID from the URL (e.g., `2210.03629` from `https://arxiv.org/abs/2210.03629`)

### 2. Choose a file name

Use the format `{short-descriptive-name}-{year}`:
- Lowercase, hyphen-separated
- Include the publication year
- Example: `react-reasoning-acting-2022`

### 3. Add to the conversion script

Add the paper name and arXiv ID to the `PAPERS` dict in `scripts/convert_papers.py`:

```python
PAPERS = {
    # ... existing papers ...
    "new-paper-name-2024": "2401.12345",
}
```

### 4. Run the conversion

```bash
# Convert all papers (skips already-downloaded/converted ones)
python scripts/convert_papers.py

# Convert only papers matching a prefix
python scripts/convert_papers.py new-paper
```

The script will:
- Download the PDF to `papers/pdfs/{name}.pdf` (skips if exists)
- Convert to Markdown at `papers/docling/{name}.md` using Docling (skips if exists)

### 5. Manual conversion (alternative)

```python
from docling.document_converter import DocumentConverter

converter = DocumentConverter()
result = converter.convert("papers/pdfs/some-paper-2024.pdf")
md_text = result.document.export_to_markdown()

with open("papers/docling/some-paper-2024.md", "w") as f:
    f.write(md_text)
```

## Important Rules

- **File naming must match** across `papers/pdfs/` and `papers/docling/` — same base name, different extensions (`.pdf` vs `.md`).
- **Do not manually write or synthesize paper summaries from memory.** All Markdown content in `papers/docling/` must be machine-extracted from the actual PDF files using Docling.
- **Docling limitations are expected**: Mathematical formulas appear as `<!-- formula-not-decoded -->` placeholders. Images appear as `<!-- image -->` placeholders.
- **PDF download URL pattern**: `https://arxiv.org/pdf/{arxiv_id}`

## Dependencies

Docling is installed in the project's virtual environment:
```bash
source .venv/bin/activate
pip install docling
```
