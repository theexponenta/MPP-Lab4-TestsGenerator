namespace TestsGenerator.Tests;

using TestsGenerator.Core;
using System.Threading.Tasks.Dataflow;


public class TestsPipeline
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 4, 5)]
    [InlineData(100, 100, 100)]
    public void TestPipeline(int maxRead, int maxGenerate, int maxWrite)
    {
        var handler = new MockOutputHandler(maxWrite);
        var generator = new PipelineTestsGenerator(maxGenerate, handler);

        var readFilesBlock = new TransformBlock<string, string>(
            ReadFile,
            new ExecutionDataflowBlockOptions{ MaxDegreeOfParallelism = maxRead }
        );

        generator.Link(readFilesBlock, new DataflowLinkOptions { PropagateCompletion = true });

        var directoryInfo = new DirectoryInfo("TestClasses");
        foreach (var fileInfo in directoryInfo.EnumerateFiles())
        {
            readFilesBlock.Post(Path.Join(directoryInfo.Name, fileInfo.Name));
        }

        readFilesBlock.Complete();
        generator.WaitForCompletion();

        Assert.Equal(6, handler.ResultsCount);
    }

    private static async Task<string> ReadFile(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }
}
