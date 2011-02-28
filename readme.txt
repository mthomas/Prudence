Prudence is a log searching and aggregation system for windows. Prudence is designed for simplicity and built on top of existing, robust frameworks.

Structure

* Prudence.Core: Library containing all functionality of Prudence
* Prudence.Indexer: Console application that hosts the indexing process
* Prudence.Forwarder: Console applications that hosts the forwarder process
* Prudence.Web: Web application that exposes search functionality
* Prudence.Generator: A demo application that generates copious log files

Technologies

* Lucene.NET for full text search
* Windows File Shares for data transfer