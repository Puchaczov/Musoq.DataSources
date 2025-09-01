# Musoq Plugin Development Resources

This directory contains comprehensive resources for developing custom Musoq plugins, allowing you to query any data source using SQL-like syntax.

## Available Resources

### 📖 [Plugin Development Guide](PLUGIN_DEVELOPMENT_GUIDE.md)
A comprehensive, in-depth guide covering all aspects of Musoq plugin development:
- Complete architecture overview
- Step-by-step implementation walkthrough
- Advanced features and patterns
- Testing strategies
- Best practices and optimization tips
- Real-world examples

**Best for:** Developers who want to understand the complete plugin ecosystem and build complex, production-ready plugins.

### ⚡ [Quick Start Template](PLUGIN_QUICK_START.md)
A minimal, ready-to-use template that gets you up and running in 5 minutes:
- Pre-built project structure
- Copy-paste code templates
- Simple working example
- Common modification patterns

**Best for:** Developers who want to start quickly with a working foundation and learn by doing.

### 🎯 [Working Example Plugin](Musoq.DataSources.Example/)
A complete, functional plugin implementation demonstrating:
- Proper project structure
- All required components
- Custom functions
- Multiple constructor patterns
- Documentation best practices

**Best for:** Developers who learn best by examining working code and want a reference implementation.

## Getting Started

### Option 1: Quick Start (Recommended for beginners)
1. Read the [Quick Start Template](PLUGIN_QUICK_START.md)
2. Copy the template code to your project
3. Customize for your data source
4. Build and test

### Option 2: Comprehensive Learning
1. Read the [Plugin Development Guide](PLUGIN_DEVELOPMENT_GUIDE.md) thoroughly
2. Examine the [Example Plugin](Musoq.DataSources.Example/) source code
3. Implement your plugin following the detailed patterns
4. Reference the guide for advanced features

### Option 3: Reference-Based Development
1. Study the [Working Example Plugin](Musoq.DataSources.Example/)
2. Use it as a template for your implementation
3. Refer to the [Plugin Development Guide](PLUGIN_DEVELOPMENT_GUIDE.md) for specific questions
4. Consult existing plugins in this repository for additional patterns

## Plugin Architecture Overview

A Musoq plugin consists of these core components:

```
YourPlugin/
├── AssemblyInfo.cs              # Plugin registration
├── YourSchema.cs                # Main schema class
├── Entities/
│   └── YourEntity.cs           # Data model
├── Tables/
│   ├── YourTable.cs            # Table metadata
│   └── YourTableHelper.cs      # Column mappings
├── Sources/
│   └── YourRowSource.cs        # Data retrieval logic
├── YourLibrary.cs              # Custom functions (optional)
└── YourPlugin.csproj           # Project configuration
```

## Common Use Cases

### 🌐 Web API Integration
Query REST APIs, GraphQL endpoints, or web services using SQL syntax.

### 🗄️ Custom Database Connectors
Connect to proprietary databases or data stores not supported by standard providers.

### 📁 File System Operations
Query files, directories, logs, or any file-based data sources.

### ☁️ Cloud Service Integration
Query cloud services like AWS, Azure, GCP resources, or SaaS platforms.

### 🔧 System Monitoring
Query system metrics, performance counters, or monitoring data.

### 📊 Data Processing Pipelines
Transform and query data from ETL processes or data pipelines.

## Example Usage

Once you create a plugin (e.g., "myplugin"), you can query it like this:

```sql
-- Basic query
SELECT * FROM #myplugin.datasource()

-- With parameters
SELECT * FROM #myplugin.datasource('connection-string', 100)

-- Using custom functions
SELECT Id, Name, CustomFunction(Name, 'prefix') as Formatted
FROM #myplugin.datasource()
WHERE Value > 50
ORDER BY CreatedDate DESC

-- Joining with other sources
SELECT p.Name, s.SystemInfo
FROM #myplugin.products() p
JOIN #system.dual() s ON 1=1
```

## Available Plugins in This Repository

Study these existing plugins for real-world examples:

- **OpenAI** - LLM integration with custom AI functions
- **Docker** - Container, image, network, and volume management
- **Kubernetes** - k8s resource queries
- **System** - System utilities and range generators
- **Postgres** - Database connectivity patterns
- **Git** - Version control system integration
- **Time** - Date/time manipulation utilities

## Plugin Development Workflow

1. **Plan Your Schema**: Define what tables and functions you need
2. **Design Your Entity**: Model your data structure
3. **Implement Core Components**: Schema, Table, RowSource, Entity
4. **Add Custom Functions**: Extend with domain-specific operations
5. **Test Thoroughly**: Unit tests, integration tests, real-world usage
6. **Document**: Add comprehensive documentation and examples
7. **Package**: Create NuGet package for distribution

## Best Practices

- ✅ Follow existing naming conventions
- ✅ Include comprehensive XML documentation
- ✅ Handle errors gracefully
- ✅ Support cancellation tokens
- ✅ Use async/await for I/O operations
- ✅ Implement proper resource disposal
- ✅ Provide meaningful examples
- ✅ Test with various data sizes
- ✅ Consider performance implications
- ✅ Support configuration through environment variables

## Support and Community

- **Issues**: Report bugs or request features in the GitHub repository
- **Discussions**: Join the community discussions for questions and ideas
- **Examples**: Check existing plugins for patterns and inspiration
- **Documentation**: Contribute to improving these guides

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Ready to build your first Musoq plugin?** Start with the [Quick Start Template](PLUGIN_QUICK_START.md) and have a working plugin in minutes!