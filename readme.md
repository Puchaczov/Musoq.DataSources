# Musoq.DataSources

This project contains data sources for Musoq engine. Musoq data sources are plugins that allows musoq engine to treat external data sources as tables.

## 🚀 Create Your Own Plugin

Want to create a custom plugin for Musoq? We've got you covered! Check out our comprehensive plugin development resources:

- **[📖 Plugin Development Guide](PLUGIN_DEVELOPMENT_GUIDE.md)** - Complete guide with architecture, patterns, and best practices
- **[⚡ Quick Start Template](PLUGIN_QUICK_START.md)** - Get a working plugin in 5 minutes
- **[🎯 Example Plugin](Musoq.DataSources.Example/)** - Full working example for reference
- **[📋 Development Overview](PLUGIN_DEVELOPMENT_README.md)** - Resource overview and getting started guide

Whether you're integrating APIs, databases, files, or any other data source, these guides will help you build powerful, SQL-queryable plugins for Musoq.

# Data sources

- Airtable (allows to query tables from Airtable)
- Archives (allows to treat archives as tables)
- CANBus (allows to treat CAN .dbc files and corresponding .csv files that contains records of a CAN bus as tables)
- Docker (allows to treat docker containers, images, etc as tables)
- Example (demonstration plugin showing plugin development patterns)
- FlatFile (allows to treat flat files as table)
- Json (allows to treat json files as tables)
- Kubernetes (allows to treat kubernetes pods, services, etc as tables)
- OpenAI (exists mainly to be combined with other plugins to allow fuzzy search by GPT models)
- Postgres (allows to treat postgres database as tables)
- SeparatedValues (allows to treat separated values files as tables)
- Sqlite (allows to treat sqlite database as tables)
- System (mostly utils, ranges and dual table resides here)
- Time (allows to treat time as table)
- Roslyn (allows to query C# code)

### To look at the engine itself go to [Musoq](https://github.com/Puchaczov/Musoq) repository.
