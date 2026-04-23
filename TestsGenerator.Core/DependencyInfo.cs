namespace TestsGenerator.Core;

internal class DependencyInfo
{
    public string TypeName { get; }
 
    public string ParamName { get; }
 
    public string MockFieldName { get; }
 
    public bool IsInterface { get; }
 
    public DependencyInfo(string typeName, string paramName, bool isInterface)
    {
        TypeName    = typeName;
        ParamName   = paramName;
        IsInterface = isInterface;
        MockFieldName = $"_{char.ToLower(paramName[0])}{paramName[1..]}Mock";
    }
}
