using System.Threading.Tasks.Dataflow;

namespace TestsGenerator.Core;

public class FileSystemOutputHandler : IGeneratorOutputHandler
{
    private string _directoryPath;
    private ActionBlock<GeneratorResult<GeneratorOutput, Exception>> _writingBlock;
    private PipelineTestsGenerator? _parentGenerator = null;

    public FileSystemOutputHandler(string directoryPath, int maxTasks)
    {
        _directoryPath = directoryPath;
        _writingBlock = new(WriteFile, new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = maxTasks});
    }

    public void Link(ISourceBlock<GeneratorResult<GeneratorOutput, Exception>> sourceBlock, DataflowLinkOptions linkOptions)
    {
        sourceBlock.LinkTo(_writingBlock, linkOptions);
    }

    public void SetParentGenerator(PipelineTestsGenerator generator)
    {
        _parentGenerator = generator;
    }

    private async Task WriteFile(GeneratorResult<GeneratorOutput, Exception> result)
    {
        if (result.Result != null)
        {
            string filePath = Path.Join(_directoryPath, $"{result.Result.ClassName}Tests.cs");

            try
            {
                await File.WriteAllTextAsync(filePath, result.Result.Source);
            } catch (Exception e)
            {
                if (_parentGenerator != null)
                {
                    _parentGenerator.AddError(e, PipelineGeneratorStage.OUTPUT);
                }
            }
        }
    }

    public void WaitForCompletion()
    {
        _writingBlock.Completion.Wait();    
    }
}
