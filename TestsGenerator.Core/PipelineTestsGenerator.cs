using System.Threading.Tasks.Dataflow;

namespace TestsGenerator.Core;

public class PipelineTestsGenerator
{
    TransformManyBlock<string, GeneratorOutput> _generatorBlock;
    IGeneratorOutputHandler _outputHandler;

    public PipelineTestsGenerator(int maxTasks, IGeneratorOutputHandler outputHandler)
    {
        _generatorBlock = new(TestsGenerator.GenerateTests, new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = maxTasks});
        _outputHandler = outputHandler;
        _outputHandler.Link(_generatorBlock, new DataflowLinkOptions{PropagateCompletion = true});
    }

    public void Link(ISourceBlock<string> sourceBlock, DataflowLinkOptions linkOptions)
    {
        sourceBlock.LinkTo(_generatorBlock, linkOptions);
    }   

    public void WaitForCompletion()
    {
        _outputHandler.WaitForCompletion();
    }
}
