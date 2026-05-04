namespace TestsGenerator.Core;

public record GeneratorResult<TResult, TError>(TResult? Result, TError? Error);
