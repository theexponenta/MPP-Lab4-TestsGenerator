namespace TestsGenerator.Core;

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

public static class TestsGenerator
{

    public static IEnumerable<GeneratorOutput> GenerateTests(string source)
    { 
        SyntaxTree? syntaxTree = CSharpSyntaxTree.ParseText(source);
        if (syntaxTree == null)
        {
            yield break;
        }

        var root = syntaxTree.GetCompilationUnitRoot();
        var originalUsings = root.Usings;
 
        var classDeclarations = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));
 
        foreach (var classDecl in classDeclarations)
        {
            var className = classDecl.Identifier.Text;
            string? nsName = GetNamespace(classDecl);
 
            var ctorParams = GetConstructorParameters(classDecl);
            var dependencies  = ctorParams
                .Select(p => new DependencyInfo(
                    p.Type!.ToString(),
                    p.Identifier.Text,
                    IsInterfaceName(p.Type!.ToString()))
                ).ToList();
 
            bool hasMocks = dependencies.Any(d => d.IsInterface);
 
            var publicMethods = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Where(m => m.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                .ToList();
 
            var methodOverloads = publicMethods
                .GroupBy(m => m.Identifier.Text)
                .ToList();
 
            string sutField = $"_{char.ToLower(className[0])}{className[1..]}";
 
            var classMembers = new List<MemberDeclarationSyntax>();
            classMembers.Add(BuildField(className, sutField)); 
            foreach (var dep in dependencies.Where(d => d.IsInterface))
            {
                classMembers.Add(BuildMockField(dep.TypeName, dep.MockFieldName));
            }
 
            classMembers.Add(
                BuildSetUpConstructor($"{className}Tests", className, sutField, dependencies)
            );
 
            foreach (var overload in methodOverloads)
            {
                var methods = overload.ToList();
                bool overloaded = methods.Count > 1;
 
                for (int i = 0; i < methods.Count; i++)
                {
                    string testName = overloaded
                        ? $"{overload.Key}{i + 1}Test"
                        : $"{overload.Key}Test";
 
                    classMembers.Add(BuildTestMethod(testName, methods[i], sutField));
                }
            }
 
            string testClassName = $"{className}Tests";
 
            var testClass = ClassDeclaration(testClassName)
                .AddModifiers(Token(SyntaxKind.PublicKeyword))
                .AddMembers(classMembers.ToArray());
 
            var usings = BuildUsings(originalUsings, nsName, hasMocks);
 
            string testNs = nsName != null ? $"{nsName}.Tests" : "Tests";
 
            var cu = CompilationUnit()
                .AddUsings(usings.ToArray())
                .AddMembers(
                    NamespaceDeclaration(ParseName(testNs))
                        .AddMembers(testClass))
                .NormalizeWhitespace();
 
            yield return new GeneratorOutput(className, cu.ToFullString());
        }
    }

    private static FieldDeclarationSyntax BuildField(string typeName, string fieldName) =>
        FieldDeclaration(
                VariableDeclaration(IdentifierName(typeName))
                    .AddVariables(VariableDeclarator(fieldName)))
            .AddModifiers(Token(SyntaxKind.PrivateKeyword));
 
    private static FieldDeclarationSyntax BuildMockField(string ifaceType, string fieldName)
    {
        var mockType = GenericName(Identifier("Mock"))
            .AddTypeArgumentListArguments(IdentifierName(ifaceType));
 
        return FieldDeclaration(
                VariableDeclaration(mockType)
                    .AddVariables(VariableDeclarator(fieldName)))
            .AddModifiers(Token(SyntaxKind.PrivateKeyword));
    }
 
    private static ConstructorDeclarationSyntax BuildSetUpConstructor(
        string testClassName,
        string className,
        string sutField,
        List<DependencyInfo> dependencies
    )
    {
        var stmts = new List<StatementSyntax>();
 
        foreach (var dep in dependencies)
        {
            if (!dep.IsInterface)
                continue;

            var mockType = GenericName(Identifier("Mock"))
                .AddTypeArgumentListArguments(IdentifierName(dep.TypeName));
 
            stmts.Add(ExpressionStatement(
                AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    IdentifierName(dep.MockFieldName),
                    ObjectCreationExpression(mockType).WithArgumentList(ArgumentList()))));
        }
 
        var ctorArgs = dependencies.Select(dep =>
            dep.IsInterface
                ? Argument(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName(dep.MockFieldName),
                        IdentifierName("Object")
                    )
                )
                : Argument(DefaultExpression(IdentifierName(dep.TypeName)))
        ).ToArray();
 
        stmts.Add(ExpressionStatement(
            AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                IdentifierName(sutField),
                ObjectCreationExpression(IdentifierName(className))
                    .WithArgumentList(ArgumentList(SeparatedList(ctorArgs))))));
 
        return ConstructorDeclaration(testClassName)
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .WithBody(Block(stmts));
    }
 
    private static MethodDeclarationSyntax BuildTestMethod(
        string testName,
        MethodDeclarationSyntax method,
        string sutField
    )
    {
        return MethodDeclaration(
                PredefinedType(Token(SyntaxKind.VoidKeyword)),
                Identifier(testName))
            .AddModifiers(Token(SyntaxKind.PublicKeyword))
            .AddAttributeLists(
                AttributeList(SingletonSeparatedList(
                    Attribute(IdentifierName("Fact")))))
            .WithBody(BuildAAABody(method, sutField));
    }
 
    private static BlockSyntax BuildAAABody(MethodDeclarationSyntax method, string sutField)
    {
        var stmts = new List<StatementSyntax>();
        var parameters = method.ParameterList.Parameters;
        bool isVoid = IsVoidReturn(method.ReturnType);
        string retType = method.ReturnType.ToString();
 
        stmts.Add(LineComment("Arrange"));
 
        foreach (var p in parameters)
        {
            string pType = p.Type!.ToString();
            string pName = p.Identifier.Text;
 
            stmts.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName(pType))
                    .AddVariables(
                        VariableDeclarator(pName)
                            .WithInitializer(EqualsValueClause(DefaultValueFor(pType))))));
        }
 
        stmts.Add(LineComment("Act"));
 
        var callArgs = SeparatedList(
            parameters.Select(p => Argument(IdentifierName(p.Identifier.Text)))
        );
 
        var call = InvocationExpression(
            MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                IdentifierName(sutField),
                IdentifierName(method.Identifier.Text)),
            ArgumentList(callArgs));
 
        if (isVoid)
        {
            stmts.Add(ExpressionStatement(call));
        }
        else
        {
            stmts.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName(retType))
                    .AddVariables(
                        VariableDeclarator("actual")
                            .WithInitializer(EqualsValueClause(call)))));
        }
 
        stmts.Add(LineComment("Assert"));
 
        if (!isVoid)
        {
            stmts.Add(LocalDeclarationStatement(
                VariableDeclaration(IdentifierName(retType))
                    .AddVariables(
                        VariableDeclarator("expected")
                            .WithInitializer(EqualsValueClause(DefaultValueFor(retType))))));
 
            stmts.Add(ExpressionStatement(
                InvocationExpression(
                    MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        IdentifierName("Assert"),
                        IdentifierName("Equal")),
                    ArgumentList(SeparatedList(new[]
                    {
                        Argument(IdentifierName("expected")),
                        Argument(IdentifierName("actual"))
                    })))));
        }

        stmts.Add(FailAssertion());
 
        return Block(stmts);
    }
 
    private static IReadOnlyList<ParameterSyntax> GetConstructorParameters(ClassDeclarationSyntax classDecl)
    {
        return classDecl.Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
            .OrderByDescending(c => c.ParameterList.Parameters.Count)
            .FirstOrDefault()
            ?.ParameterList.Parameters
            .ToList()
            ?? (IReadOnlyList<ParameterSyntax>)Array.Empty<ParameterSyntax>();
    }
 
    private static bool IsInterfaceName(string typeName)
    {
        return typeName.Length >= 2 && typeName[0] == 'I' && char.IsUpper(typeName[1]);
    }
     private static bool IsVoidReturn(TypeSyntax returnType) =>
        returnType is PredefinedTypeSyntax p && p.Keyword.IsKind(SyntaxKind.VoidKeyword);
 
    private static ExpressionSyntax DefaultValueFor(string typeName) {
        return typeName.TrimEnd('?') switch
        {
            "string" or "String"  => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal("")),
            "bool" or "Boolean" => LiteralExpression(SyntaxKind.FalseLiteralExpression),
            "char" or "Char" => LiteralExpression(SyntaxKind.CharacterLiteralExpression, Literal('\0')),

            "byte" or "Byte"
          or "sbyte"  or "SByte"
          or "short"  or "Int16"
          or "ushort" or "UInt16"
          or "int"    or "Int32"
          or "uint"   or "UInt32"
          or "long"   or "Int64"
          or "ulong"  or "UInt64"
          or  "float"   or "Single"
          or  "double"  or "Double"
          or  "decimal" or "Decimal" => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)),

            _ => DefaultExpression(IdentifierName(typeName))
        };
    }

     private static StatementSyntax FailAssertion() =>
        ExpressionStatement(
            InvocationExpression(
                MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    IdentifierName("Assert"),
                    IdentifierName("True")),
                ArgumentList(SeparatedList(new[]
                {
                    Argument(LiteralExpression(SyntaxKind.FalseLiteralExpression)),
                    Argument(LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        Literal("autogenerated")))
                }))));
 
    private static StatementSyntax LineComment(string section) =>
        EmptyStatement()
            .WithSemicolonToken(
                Token(
                    TriviaList(Comment($"// {section}")),
                    SyntaxKind.SemicolonToken,
                    TriviaList(ElasticLineFeed)));
 
    private static string? GetNamespace(ClassDeclarationSyntax classDecl)
    {
        SyntaxNode? node = classDecl.Parent;
        while (node != null)
        {
            if (node is NamespaceDeclarationSyntax ns) 
            {
                return ns.Name.ToString();
            }

            if (node is FileScopedNamespaceDeclarationSyntax fsns) 
            {
                return fsns.Name.ToString();
            }

            node = node.Parent;
        }

        return null;
    }

    private static List<UsingDirectiveSyntax> BuildUsings(
        SyntaxList<UsingDirectiveSyntax> original,
        string? namespaceName,
        bool hasMocks)
    {
        var usings = original
            .Select(u => u.WithoutTrivia())
            .ToList();
 
        void Add(string ns)
        {
            if (!usings.Any(u => u.Name?.ToString() == ns))
                usings.Add(UsingDirective(ParseName(ns)));
        }
 
        Add("Xunit");
        if (hasMocks) Add("Moq");
        if (namespaceName != null) Add(namespaceName);
 
        return usings;
    }
}
