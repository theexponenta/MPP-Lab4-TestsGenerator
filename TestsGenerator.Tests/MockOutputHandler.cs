namespace TestsGenerator.Tests;

using System.Threading.Tasks.Dataflow;
using TestsGenerator.Core;

public class MockOutputHandler : IGeneratorOutputHandler
{
    private ActionBlock<GeneratorResult<GeneratorOutput, Exception>> _writingBlock;
    private PipelineTestsGenerator? _parentGenerator = null;

    private int _resultsCount = 0;
    private bool _withException;

    public int ResultsCount {get => _resultsCount;}

    public MockOutputHandler(int maxTasks, bool withException = false)
    {
        _withException = withException;
        _writingBlock = new(HandleResult, new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = maxTasks});
    }
    
    public void Link(ISourceBlock<GeneratorResult<GeneratorOutput, Exception>> sourceBlock, DataflowLinkOptions linkOptions)
    {
        sourceBlock.LinkTo(_writingBlock, linkOptions);
    }

    public void SetParentGenerator(PipelineTestsGenerator generator)
    {
        _parentGenerator = generator;
    }

    public void WaitForCompletion()
    {
        _writingBlock.Completion.Wait();    
    }

    private void HandleResult(GeneratorResult<GeneratorOutput, Exception> output)
    {
        if (_withException)
        {
            if (_parentGenerator != null)
            {
                _parentGenerator.AddError(new StackOverflowException(), PipelineGeneratorStage.OUTPUT);
            }
        }
        else
        {
            Interlocked.Increment(ref _resultsCount);
        }
    }
}

