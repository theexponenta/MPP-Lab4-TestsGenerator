namespace TestsGenerator.Application;

using System.Threading.Tasks.Dataflow;
using TestsGenerator.Core;

class Program
{
    private readonly record struct AppOptions(
        string OutputDirectory,
        int MaxReadTasks,
        int MaxGenerateTasks,
        int MaxWriteTasks,
        List<string> InputFiles
    );
    
    private const int ExitCodeInvalidArgs = 2;

    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            return 0;
        }

        if (!TryParseArgs(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            return ExitCodeInvalidArgs;
        }

        Directory.CreateDirectory(options.OutputDirectory);

        var writer = new FileSystemOutputHandler(options.OutputDirectory, options.MaxWriteTasks);
        var generator = new PipelineTestsGenerator(options.MaxGenerateTasks, writer);

        var readFilesBlock = new TransformBlock<string, string>(
            ReadFileAsStringAsync,
            new ExecutionDataflowBlockOptions{ MaxDegreeOfParallelism = options.MaxReadTasks }
        );

        generator.Link(readFilesBlock, new DataflowLinkOptions { PropagateCompletion = true });

        foreach (var filePath in options.InputFiles)
        {
            readFilesBlock.Post(filePath);
        }

        readFilesBlock.Complete();

        generator.WaitForCompletion();
        return 0;
    }

    private static async Task<string> ReadFileAsStringAsync(string filePath)
    {
        return await File.ReadAllTextAsync(filePath);
    }

    private static bool TryParseArgs(
        string[] args,
        out AppOptions options,
        out string error)
    {
        options = default;
        error = string.Empty;

        string? outDir = null;
        int? read = null;
        int? gen = null;
        int? write = null;

        var files = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a is "--out")
            {
                if (!TryGetString(args, ref i, out outDir))
                {
                    error = "--out requires a value";
                    return false;
                }
            }
            else if (a is "--read")
            {
                if (!TryGetInt(args, ref i, out read))
                {
                    error = "--read requires an integer value";
                    return false;
                }
            }
            else if (a is "--gen")
            {
                if (!TryGetInt(args, ref i, out gen))
                {
                    error = "--gen requires an integer value";
                    return false;
                }
            }
            else if (a is "--write")
            {
                if (!TryGetInt(args, ref i, out write))
                {
                    error = "--write requires an integer value";
                    return false;
                }
            }
            else if (a.StartsWith('-'))
            {
                error = $"Unknown option: {a}";
                return false;
            }
            else
            {
                files.Add(a);
            }
        }

        if (string.IsNullOrWhiteSpace(outDir))
        {
            error = "Missing required option: --out <dir>";
            return false;
        }
        if (read is null || read <= 0)
        {
            error = "--read must be a positive integer";
            return false;
        }
        if (gen is null || gen <= 0)
        {
            error = "--gen must be a positive integer";
            return false;
        }
        if (write is null || write <= 0)
        {
            error = "--write must be a positive integer";
            return false;
        }
        if (files.Count == 0)
        {
            error = "No input files provided";
            return false;
        }

        var fullFiles = files.Select(Path.GetFullPath).ToList();
        var missing = fullFiles.Where(f => !File.Exists(f)).ToList();
        if (missing.Count > 0)
        {
            error = "Input file(s) not found:\n" + string.Join('\n', missing);
            return false;
        }

        options = new AppOptions(
            OutputDirectory: Path.GetFullPath(outDir),
            MaxReadTasks: read.Value,
            MaxGenerateTasks: gen.Value,
            MaxWriteTasks: write.Value,
            InputFiles: fullFiles
        );
        
        error = string.Empty;
        return true;

        static bool TryGetString(string[] a, ref int i, out string? value)
        {
            value = null;
            if (i + 1 >= a.Length) 
            {
                return false;
            }

            value = a[++i];
            return true;
        }

        static bool TryGetInt(string[] a, ref int i, out int? value)
        {
            value = null;
            if (i + 1 >= a.Length) 
            {
                return false;
            }

            var raw = a[++i];
            if (!int.TryParse(raw, out var parsed)) 
            {
                return false;
            }

            value = parsed;
            return true;
        }
    }
}

