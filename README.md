# ExecutionFrameExporter.NET

Based on the Microsoft NET Framework Command-Line Debugger Sample (MDbg Sample), ExecutionFrameExporter.NET initiates a debugging session on a .NET application to exports all execution frames step by step.

## Goals

Debugging is often a time consumming activity. While some bugs are relatively easy to catch, others are more insiduous. How to demontrates a race condition on an 1MLoc+ app ? Where does this uncaught exception come from ? Oh, I forgot to step in, I need to start over again... 

The main idea behind the execution frame exporter is a driven "Step-In" debugging tool, retrieving all useful information from the current execution frame in all active threads (MT implementation on progress):
* source line and position 
* current exception
* current method and its parameters
* local variable values
* global variable values

These exported frames should help to :
* have a complete history of program executions or record test cases
* create method execution trees
* create execution and sequence diagrams
* locate origin of uncaught exception
* locate race conditions / locks
* ...

## Perimeter

ExecutionFrameExporter.NET is only an exporter. Current available outputs are :
* JSON txt file
* RabbitMQ (to be implemented)

It means that you will need at least a visualisation tool to deal with the JSON exports, and most likely a NoSQL DB with an ETL tool plugged to RabbitMQ.

## Technical

MDbg is built on top of Common Language Runtime ([CLR](https://docs.microsoft.com/en-us/dotnet/standard/clr)) [debugging COM API](https://www.microsoftpressstore.com/articles/article.aspx?p=2201303&seqNum=3). 
