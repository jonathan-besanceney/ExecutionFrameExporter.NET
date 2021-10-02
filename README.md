# ExecutionFrameExporter.NET

Experiment based on Microsoft .NET Framework Command-Line Debugger Sample - MDbg Sample. It starts a debugging session on a .NET app and exports all execution steps/frames.

## Goal

Debugging is often a time-consuming activity. While some bugs are easy to fix, others are more tricky. How to demonstrate a race condition on an 1MLoc+ app ? Where does this uncaught exception come from ? Oh! forgot to step in, need to start again...
There are plenty of software to make your debugger life easier you will say. Yes, but I did not find the perfect fit to handle MSIL, all thread at once.  

The idea is to make available useful execution step information for all active threads:
* source line and position
* current exception
* current method and its parameters
* local variable values
* literal and static variable values

Once processed with the tool of your choice, these exported frames should help to:
* have a complete or user defined history of program execution - users can define their own breakpoints
* create method execution trees
* create execution and sequence diagrams
* locate origin of uncaught exception
* locate race conditions / locks
* ... (your ideas go there)

## Next Steps

* Optimize MDbg engine. This is ~~currently~~ not only the biggest bottleneck. 
    - It might be fun to use ExecutionFrameExporter to record ExecutionFrameExporter execution : Yes it is! several issues prevents recording to be completed :
        + Variable values retrieval for each frame is not optimal. CorValueBreakpoint should be tested to see if it's possible to retrieve only changed variable values.
        + Current JSON frame recording are too big to be useful. This questions NoSQL storage idea. Would a relational schema be more efficient ?
        + ValueInfo should be refactored to store complex types
        + AssemblyMgr should be used inside MDbg to avoid unnecessary COM API calls in MDbgFunction and allow BreakpointMgr to detect non-bindable delegate methods (currently skipped using IsBound property...)

* Study thread scheduling. Currently, nothing is done on that topic. https://docs.microsoft.com/en-us/dotnet/standard/threading/scheduling-threads 
* Attach ExecutionFrameExporter to a running process is not yet tested
* Mesure execution time
* Implement RabbitMQ Output?

## Technical

MDbg is built on top of Common Language Runtime ([CLR](https://docs.microsoft.com/en-us/dotnet/standard/clr)) [debugging COM API](https://www.microsoftpressstore.com/articles/article.aspx?p=2201303&seqNum=3).

## Usage

FrameExporter :

    -sourceDir=Path\to\source
    -symbolDir=Path\to\pdb
    -attach=pid
    -run=Path\to\bin
    -args="..."
    -bp="Path\to\sourcefile#startLine"[,"Path\to\sourcefile#endLine"]
    -stopOnNewThread
    -sessionName=MyAutomatedDebugSession
