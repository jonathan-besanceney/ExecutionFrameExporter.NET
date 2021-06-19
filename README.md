# ExecutionFrameExporter.NET

ExecutionFrameExporter.NET is a proof-of-concept/experiment based on the Microsoft NET Framework Command-Line Debugger Sample (MDbg Sample), ExecutionFrameExporter.NET initiates a debugging session on a .NET application to exports all execution frames.

## Goals

Debugging is often a time consumming activity. While some bugs are relatively easy to catch, others are more insiduous. How to demontrates a race condition on an 1MLoc+ app ? Where does this uncaught exception come from ? Oh! forgot to step in, need to start again...

The main idea behind the execution frame exporter is a driven debugging tool, retrieving all useful information from the current execution frame in all active threads:
* source line and position
* current exception
* current method and its parameters
* local variable values
* litteral and static variable values

These exported frames should help to :
* have a complete or user defined history of program execution : user can define custom breakpoints
* create method execution trees
* create execution and sequence diagrams
* locate origin of uncaught exception
* locate race conditions / locks
* ...

## Next Steps
* Optimize MDbg internals, this is currently the bigger bottleneck (might be fun to use ExecutionFrameExporter to record ExecutionFrameExporter execution)
* Create Unit Tests
* Attach ExecutionFrameExporter to a running process is not yet tested
* Mesure execution time
* Implement RabbitMQ Output

## Perimeter

ExecutionFrameExporter.NET is only an execution frame exporter. Current available outputs are :
* JSON txt file
* RabbitMQ (to be implemented)

It means that you will need at least a visualisation tool to deal with the JSON exports, and most likely a NoSQL DB with an ETL tool plugged to RabbitMQ.

## Technical

MDbg is built on top of Common Language Runtime ([CLR](https://docs.microsoft.com/en-us/dotnet/standard/clr)) [debugging COM API](https://www.microsoftpressstore.com/articles/article.aspx?p=2201303&seqNum=3).

## Usage

FrameExporter :

    -sourceDir=Path\to\source
    -symbolDir=Path\to\pdb
    -attach=pid
    -run=Path\to\bin
    -args=""...""
    -bp="Path\to\sourcefile#startLine"[,"Path\to\sourcefile#endLine"]
    -stopOnNewThread
    -sessionName=MyAutomatedDebugSession

