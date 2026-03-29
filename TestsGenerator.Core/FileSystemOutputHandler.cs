using System.Threading.Tasks.Dataflow;

namespace TestsGenerator.Core;

public class FileSystemOutputHandler : IGeneratorOutputHandler
{
    private string _directoryPath;
    private ActionBlock<GeneratorOutput> _writingBlock;

    public FileSystemOutputHandler(string directoryPath, int maxTasks)
    {
        _directoryPath = directoryPath;
        _writingBlock = new(WriteFile, new ExecutionDataflowBlockOptions{MaxDegreeOfParallelism = maxTasks});
    }

    public void Link(ISourceBlock<GeneratorOutput> sourceBlock, DataflowLinkOptions linkOptions)
    {
        sourceBlock.LinkTo(_writingBlock, linkOptions);
    }

    private async Task WriteFile(GeneratorOutput output)
    {
        string filePath = Path.Join(_directoryPath, $"{output.ClassName}Tests.cs");
        await File.WriteAllTextAsync(filePath, output.Source);
    }

    public void WaitForCompletion()
    {
        _writingBlock.Completion.Wait();    
    }
}
