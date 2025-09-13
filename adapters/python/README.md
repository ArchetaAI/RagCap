# RagCap Python Adapter

This package provides a thin Python wrapper around the RagCap CLI.

## Installation

```bash
pip install -e .
```

## Usage

```python
from ragcap import Capsule

# Open a RagCap capsule
cap = Capsule("knowledge.ragcap")

# Search the capsule
results = cap.search("neural networks")
print(results)

# Ask a question
answer = cap.ask("What are neural networks?")
print(answer)

# Export the capsule
cap.export("knowledge.parquet", format="parquet")
```
