namespace TestsGenerator.Tests;

using System.Threading.Tasks.Dataflow;
using TestsGenerator.Core;

public class MockOutputHandler : IGeneratorOutputHandler
{
    private ActionBlock<GeneratorOutput> _writingBlock;
    private int _resultsCount = 0;

    public int ResultsCount {get => _resultsCount;}

    public MockOutputHandler(int maxTasks)
    {
        _writingBlock = new(HandleResult, new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = maxTasks});
    }
    
    public void Link(ISourceBlock<GeneratorOutput> sourceBlock, DataflowLinkOptions linkOptions)
    {
        sourceBlock.LinkTo(_writingBlock, linkOptions);
    }

    public void WaitForCompletion()
    {
        _writingBlock.Completion.Wait();    
    }

    private void HandleResult(GeneratorOutput output)
    {
        Interlocked.Increment(ref _resultsCount);
    }
}

