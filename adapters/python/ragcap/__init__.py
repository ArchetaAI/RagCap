import subprocess
import json

class Capsule:
    def __init__(self, capsule_path):
        self.capsule_path = capsule_path

    def _run_command(self, command, *args):
        try:
            result = subprocess.run(
                ['ragcap', command, self.capsule_path] + list(args),
                capture_output=True,
                text=True,
                check=True
            )
            return result.stdout
        except FileNotFoundError:
            # Attempt to run as a dotnet tool
            result = subprocess.run(
                ['dotnet', 'ragcap', command, self.capsule_path] + list(args),
                capture_output=True,
                text=True,
                check=True
            )
            return result.stdout
        except subprocess.CalledProcessError as e:
            raise RuntimeError(f"Error executing RagCap command: {e.stderr}") from e

    def search(self, query, top_k=5, mode='hybrid'):
        output = self._run_command('search', query, '--top-k', str(top_k), '--mode', mode, '--json')
        return json.loads(output)

    def ask(self, query, top_k=5, provider='local'):
        output = self._run_command('ask', query, '--top-k', str(top_k), '--provider', provider, '--json')
        return json.loads(output)

    def export(self, export_path, format='parquet'):
        self._run_command('export', '--output', export_path, '--format', format)
        return f"Capsule exported to {export_path}"
