# Musoq Python Plugin Package Specification

This document describes the required directory structure and file format for packaging Python Data Source plugins for distribution via the Musoq CLI registry or manual installation.

## Package File Name

The final package must be a Zip archive with a filename following this pattern:

```text
{PluginName}-{Platform}-{Architecture}.zip
```

**Examples:**
- `weather-windows-x64.zip`
- `hackernews-linux-arm64.zip`
- `my-custom-plugin-alpine-x64.zip`

**Note:** Unlike .NET plugins, Python plugin names typically do not follow the `Musoq.DataSources.*` naming convention.

## Package Structure

The package uses the **same nested Zip structure** as .NET plugins. The outer zip file contains metadata files and an inner zip file holding the Python plugin files.

### Root Contents (Outer Zip)

| File | Required | Description | Content Example |
|------|----------|-------------|-----------------|
| `Plugin.zip` | Yes | The inner zip archive containing the Python plugin files. | *(Binary Data)* |
| `EntryPoint.txt` | Yes | The entry point script (must be `main.py` for v.2 plugins). | `main.py` |
| `Platform.txt` | Yes | The target operating system. | `windows`, `linux`, `macos`, or `alpine` |
| `Architecture.txt` | Yes | The target CPU architecture. | `x64` or `arm64` |
| `Version.txt` | No | The version string. Required for Python plugins (cannot be auto-detected). | `1.0.0` |
| `LibraryName.txt` | No | Display name for the plugin. If omitted, inferred from entry point. | `weather` |

### Plugin Artifacts (Inner Zip: `Plugin.zip`)

The `Plugin.zip` file must contain a v.2 Python plugin project structure.

**Required Contents:**
- `main.py` - The main plugin entry point implementing the v.2 DataPlugin contract

**Optional Contents:**
- `requirements.txt` - Python package dependencies (auto-installed on first load)
- `project.json` - Plugin metadata (optional, informational only)
- Additional `.py` modules - Supporting Python files that can be imported by `main.py`
- Any other resources the plugin needs

## Visual Hierarchy

```text
my-plugin-windows-x64.zip
├── EntryPoint.txt          # Content: "main.py"
├── Platform.txt            # Content: "windows"
├── Architecture.txt        # Content: "x64"
├── Version.txt             # Content: "1.0.0"
├── LibraryName.txt         # (Optional) Content: "my-plugin"
└── Plugin.zip              # Inner Archive
    ├── main.py                 # Required: v.2 DataPlugin implementation
    ├── requirements.txt        # Optional: Python dependencies
    ├── project.json            # Optional: Plugin metadata
    ├── helpers.py              # Optional: Supporting module
    └── utils/                  # Optional: Additional modules
        └── api_client.py
```

## Python DataPlugin v.2 Contract

The `main.py` file **MUST** implement the v.2 DataPlugin contract. Here is the complete contract specification:

```python
class DataPlugin:
    """Complete v.2 plugin contract - ALL methods are REQUIRED."""
    
    def schema_name(self) -> str:
        """Return the schema name used in SQL queries.
        
        Example: return "weather"
        SQL Usage: SELECT * FROM #weather.current()
        """
        pass
    
    def data_sources(self) -> list[str]:
        """Return list of data source method names.
        
        Example: return ["current", "forecast"]
        """
        pass
    
    def schemas(self) -> dict[str, dict[str, str]]:
        """Return dictionary mapping data source names to their column schemas.
        
        Example:
            return {
                "current": {"city": "str", "temp": "float", "timestamp": "datetime"},
                "forecast": {"city": "str", "date": "str", "high": "float", "low": "float"}
            }
        
        Supported types: "int", "str", "float", "bool", "datetime"
        """
        pass
    
    def initialize(self) -> None:
        """Initialize plugin (called once at load time)."""
        pass
    
    def get_required_env_vars(self, method_name: str) -> dict[str, bool]:
        """Return required environment variables for method.
        
        Returns: {variable_name: is_required}
                 True = required (query fails if missing)
                 False = optional (uses default)
        """
        pass
    
    def get_required_execute_arguments(self, method_name: str) -> list[tuple[str, str]]:
        """Return parameter definitions for method.
        
        Returns: [(param_name, param_type), ...]
        Example: return [("city", "str"), ("days", "int")]
        """
        pass
    
    def execute(self, method_name: str, environment_variables: dict[str, str], *args):
        """Execute data source method and yield rows.
        
        MUST be a generator (use yield, not return).
        MUST yield dictionaries with keys matching the schema.
        """
        pass
    
    def dispose(self) -> None:
        """Cleanup resources (called at unload)."""
        pass


# Module-level instance (REQUIRED)
plugin = DataPlugin()
```

## Creation Process (Example)

1. **Create the Python plugin project:**
   ```
   my_plugin/
   ├── main.py           # Implement DataPlugin contract
   ├── requirements.txt  # List dependencies (optional)
   └── project.json      # Metadata (optional)
   ```

2. **Prepare the Inner Zip:**
   - Zip the contents of your plugin directory into `Plugin.zip`.
   - The `main.py` must be at the root of the zip (not in a subdirectory).

3. **Create Metadata Files:**
   - Create `EntryPoint.txt` with content: `main.py`
   - Create `Platform.txt` with the platform (e.g., `windows`, `linux`, `alpine`, `macos`).
   - Create `Architecture.txt` with the architecture (e.g., `x64`, `arm64`).
   - Create `Version.txt` with the version string (e.g., `1.0.0`).
   - (Optional) Create `LibraryName.txt` with the display name.

4. **Create the Final Package:**
   - Zip `Plugin.zip`, `EntryPoint.txt`, `Platform.txt`, `Architecture.txt`, `Version.txt`, and any optional metadata files.
   - Name the final zip: `{plugin-name}-{platform}-{architecture}.zip`

### Example Commands (PowerShell)

```powershell
$pluginName = "weather"
$version = "1.0.0"
$platform = "windows"
$architecture = "x64"

# Create Plugin.zip from the plugin directory
Compress-Archive -Path ".\my_plugin\*" -DestinationPath ".\Plugin.zip" -Force

# Create metadata files
"main.py" | Out-File -FilePath ".\EntryPoint.txt" -Encoding utf8 -NoNewline
$platform | Out-File -FilePath ".\Platform.txt" -Encoding utf8 -NoNewline
$architecture | Out-File -FilePath ".\Architecture.txt" -Encoding utf8 -NoNewline
$version | Out-File -FilePath ".\Version.txt" -Encoding utf8 -NoNewline
$pluginName | Out-File -FilePath ".\LibraryName.txt" -Encoding utf8 -NoNewline

# Create final package
Compress-Archive -Path ".\Plugin.zip", ".\EntryPoint.txt", ".\Platform.txt", ".\Architecture.txt", ".\Version.txt", ".\LibraryName.txt" -DestinationPath ".\$pluginName-$platform-$architecture.zip" -Force

# Cleanup intermediate files
Remove-Item ".\Plugin.zip", ".\EntryPoint.txt", ".\Platform.txt", ".\Architecture.txt", ".\Version.txt", ".\LibraryName.txt"
```

### Example Commands (Bash)

```bash
PLUGIN_NAME="weather"
VERSION="1.0.0"
PLATFORM="linux"
ARCHITECTURE="x64"

# Create Plugin.zip from the plugin directory
cd my_plugin && zip -r ../Plugin.zip . && cd ..

# Create metadata files
echo -n "main.py" > EntryPoint.txt
echo -n "$PLATFORM" > Platform.txt
echo -n "$ARCHITECTURE" > Architecture.txt
echo -n "$VERSION" > Version.txt
echo -n "$PLUGIN_NAME" > LibraryName.txt

# Create final package
zip "${PLUGIN_NAME}-${PLATFORM}-${ARCHITECTURE}.zip" Plugin.zip EntryPoint.txt Platform.txt Architecture.txt Version.txt LibraryName.txt

# Cleanup intermediate files
rm Plugin.zip EntryPoint.txt Platform.txt Architecture.txt Version.txt LibraryName.txt
```

## Requirements.txt Format

If your plugin has Python dependencies, include a `requirements.txt` file in the plugin directory:

```txt
requests>=2.31.0
pandas==2.1.0
python-dateutil>=2.8.2
beautifulsoup4>=4.12.0
```

**Installation Behavior:**
- Packages are automatically installed via `pip install -r requirements.txt` during plugin discovery.
- Installation happens once per session (until server restart).
- Installation failures log warnings but don't prevent the plugin from loading.
- Each plugin's requirements are independent.

## Platform Compatibility

Python plugins are generally platform-independent, but you should create packages for each target platform when:
- Your plugin uses platform-specific Python packages
- Your plugin includes native binaries or compiled extensions
- You want to ensure proper architecture targeting

For pure-Python plugins that work on all platforms, you can create a single package and rename it for each platform/architecture combination.

## Differences from .NET Plugin Packages

| Aspect | .NET Plugins | Python Plugins |
|--------|-------------|----------------|
| Entry Point | `*.dll` file | `main.py` |
| Version Detection | Can be extracted from DLL metadata | Must be provided in `Version.txt` |
| Dependencies | Included as DLLs in Plugin.zip | Listed in `requirements.txt`, installed at runtime |
| third-party-notices | Required for license compliance | Not required |
| Platform Dependency | Highly platform-specific | Often platform-independent |

## Installation

Once you have created the package, you can install it using the Musoq CLI:

```bash
# Install from a local package (zip or extracted directory)
musoq datasource import /path/to/my-plugin-windows-x64.zip
# or
musoq datasource import /path/to/extracted/package

# Install from the built-in plugin registry
musoq datasource install my-plugin
```

### Installing from a custom registry

You can add multiple registries. The configuration is persisted by the local agent.

```bash
# Add a registry
musoq registry add custom https://your-registry.example.com/registry.json
```