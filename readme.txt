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

Internal Logging Practices

(inspired by http://commons.apache.org/logging/commons-logging-1.0.3/usersguide.html)

* DEBUG: Detailed information of execution flow. Nearly constant stream of events.
* INFO: Interesting runtime events (startup/shutdown). Be generally conservative with INFO logging.
* WARN: "Almost" errors and runtime situations that are undesirable or unexpected but immediately recoverable.
* ERROR: Runtime errors that can be recovered from.  Indicates potential bugs or serious issue with runtime.
* FATAL: Severe errors that cause premature termination.

