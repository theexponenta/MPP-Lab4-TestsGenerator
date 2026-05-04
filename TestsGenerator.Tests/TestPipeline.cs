namespace TestsGenerator.Tests;

using TestsGenerator.Core;
using System.Threading.Tasks.Dataflow;
using TestsGenerator.Core.Exceptions;

public class TestsPipeline
{
    MockOutputHandler handler;
    PipelineTestsGenerator generator;
    TransformBlock<string, GeneratorResult<SourceCode, Exception>> readFilesBlock;

    private void InitPipeline(int maxRead, int maxGenerate, int maxWrite, Func<string, Task<GeneratorResult<SourceCode, Exception>>> readMethod, bool otputException = false)
    {
        handler = new MockOutputHandler(maxWrite, otputException);
        generator = new PipelineTestsGenerator(maxGenerate, handler);
        readFilesBlock = new TransformBlock<string, GeneratorResult<SourceCode, Exception>>(
            readMethod,
            new ExecutionDataflowBlockOptions{ MaxDegreeOfParallelism = maxRead }
        );
        generator.Link(readFilesBlock, new DataflowLinkOptions { PropagateCompletion = true });
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(3, 4, 5)]
    [InlineData(100, 100, 100)]
    public void TestPipeline(int maxRead, int maxGenerate, int maxWrite)
    {
        InitPipeline(maxRead, maxGenerate, maxWrite, ReadFile);

        var directoryInfo = new DirectoryInfo("TestClasses");
        foreach (var fileInfo in directoryInfo.EnumerateFiles())
        {
            readFilesBlock.Post(Path.Join(directoryInfo.Name, fileInfo.Name));
        }

        readFilesBlock.Complete();
        generator.WaitForCompletion();

        Assert.Equal(6, handler.ResultsCount);
    }

    [Fact]
    public void TestPipelineInputException()
    {
        InitPipeline(2, 2, 2, ReadFileException);
        var directoryInfo = new DirectoryInfo("TestClasses");
        foreach (var fileInfo in directoryInfo.EnumerateFiles())
        {
            readFilesBlock.Post(Path.Join(directoryInfo.Name, fileInfo.Name));
        }

        readFilesBlock.Complete();
        generator.WaitForCompletion();

        Assert.Equal(6, generator.Errors.Count);
        foreach (var error in generator.Errors)
        {
            Assert.IsType<UnauthorizedAccessException>(error.Error);
        }
    }

    [Fact]
    public void TestPipelineGenerateException()
    {
        InitPipeline(2, 2, 2, ReadFile);
        readFilesBlock.Post("TestClasses/InvalidSyntax/InvalidSyntax.txt");
        readFilesBlock.Complete();
        generator.WaitForCompletion();

        Assert.Single(generator.Errors);
        Assert.IsType<SourceParsingError>(generator.Errors[0].Error);
    }

    [Fact]
    public void TestPipelineOutputException()
    {
        InitPipeline(2, 2, 2, ReadFile, true);
        readFilesBlock.Post("TestClasses/SinglePublicClass.cs");
        readFilesBlock.Complete();
        generator.WaitForCompletion();

        Assert.Single(generator.Errors);
        Assert.IsType<StackOverflowException>(generator.Errors[0].Error);
    }

    private static async Task<GeneratorResult<SourceCode, Exception>> ReadFileException(string filePath)
    {
        return new GeneratorResult<SourceCode, Exception>(null, new UnauthorizedAccessException());
    }

    private static async Task<GeneratorResult<SourceCode, Exception>> ReadFile(string filePath)
    {
        string source = await File.ReadAllTextAsync(filePath);
        try
        {
            return new GeneratorResult<SourceCode, Exception>(new SourceCode(filePath, source), null);    
        } catch (Exception e)
        {
            return new GeneratorResult<SourceCode, Exception>(null, e);
        }
    }
}
