const { spawn } = require('child_process');

class Capsule {
    constructor(capsulePath) {
        this.capsulePath = capsulePath;
    }

    _runCommand(command, args) {
        return new Promise((resolve, reject) => {
            const child = spawn('ragcap', [command, this.capsulePath, ...args]);

            let stdout = '';
            let stderr = '';

            child.stdout.on('data', (data) => {
                stdout += data.toString();
            });

            child.stderr.on('data', (data) => {
                stderr += data.toString();
            });

            child.on('close', (code) => {
                if (code !== 0) {
                    reject(new Error(`RagCap command failed with code ${code}: ${stderr}`));
                } else {
                    resolve(stdout);
                }
            });

            child.on('error', (err) => {
                // Try with dotnet if the command fails
                const dotnetChild = spawn('dotnet', ['ragcap', command, this.capsulePath, ...args]);
                let dotnetStdout = '';
                let dotnetStderr = '';

                dotnetChild.stdout.on('data', (data) => {
                    dotnetStdout += data.toString();
                });

                dotnetChild.stderr.on('data', (data) => {
                    dotnetStderr += data.toString();
                });

                dotnetChild.on('close', (dotnetCode) => {
                    if (dotnetCode !== 0) {
                        reject(new Error(`RagCap command failed with dotnet: ${dotnetStderr}`));
                    } else {
                        resolve(dotnetStdout);
                    }
                });

                dotnetChild.on('error', (dotnetErr) => {
                    reject(new Error(`Failed to execute RagCap command: ${err.message} and ${dotnetErr.message}`));
                });
            });
        });
    }

    async search(query, topK = 5) {
        const output = await this._runCommand('search', ['-q', query, '--top', topK.toString(), '--format', 'json']);
        return JSON.parse(output);
    }

    async ask(query) {
        const output = await this._runCommand('ask', ['-q', query, '--format', 'json']);
        return JSON.parse(output);
    }

    async export(exportPath, format = 'parquet') {
        await this._runCommand('export', ['--output', exportPath, '--format', format]);
        return `Capsule exported to ${exportPath}`;
    }
}

module.exports = { Capsule };
