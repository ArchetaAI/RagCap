# RagCap JavaScript Adapter

This package provides a thin JavaScript wrapper around the RagCap CLI.

## Installation

```bash
npm install ./ragcap-js
```

## Usage

```javascript
const { Capsule } = require('ragcap-js');

async function main() {
    // Open a RagCap capsule
    const cap = new Capsule('knowledge.ragcap');

    // Search the capsule
    const results = await cap.search('neural networks');
    console.log(results);

    // Ask a question
    const answer = await cap.ask('What are neural networks?');
    console.log(answer);

    // Export the capsule
    await cap.export('knowledge.parquet', 'parquet');
}

main();
```
