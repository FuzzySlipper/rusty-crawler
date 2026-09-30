using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace PartyRpg.Testing;

/// <summary>
/// One product project's sources, read as syntax and bound to symbols, so a law about the code is a law about what
/// the code does rather than about how it is spelled.
/// </summary>
/// <remarks>
/// <para>
/// A text scan for <c>new PartyKnowledge(</c> is evaded by the target-typed <c>new(...)</c> this code base writes,
/// matches a comment that mentions the type, and passes a rename it should have followed. This binds every name
/// and every creation to the symbol it means: a law names the type through <c>typeof</c>, which the compiler keeps
/// in step with a rename, and a construction is found however it is spelled.
/// </para>
/// <para>
/// The project is compiled from its own sources against the assemblies the suite already loads, with the implicit
/// usings the SDK adds. Source generators do not run, so a generated half is missing and the compilation has
/// errors there; nothing a law asks about lives in generated code, and every query that expects to find something
/// asserts that it did, so a binding that failed cannot pass as a clean result.
/// </para>
/// </remarks>
public sealed class SourceCode
{
    private static readonly ConcurrentDictionary<string, SourceCode> Projects = new(StringComparer.Ordinal);

    /// <summary>The usings the SDK's implicit-usings switch adds to every project that enables it.</summary>
    private const string ImplicitUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """;

    private readonly Dictionary<SyntaxTree, SemanticModel> _models = [];

    private SourceCode(string project)
    {
        Project = project;
        string directory = $"src/{project}";
        List<SyntaxTree> trees = [];
        foreach (string file in Repository.Files(directory, "*.cs"))
        {
            trees.Add(CSharpSyntaxTree.ParseText(
                File.ReadAllText(file),
                new CSharpParseOptions(LanguageVersion.Preview),
                path: Repository.Relative(file)));
        }

        Assert.True(trees.Count > 0, $"{directory} holds no C# source, so a law over it would pass vacuously.");
        Trees = trees;
        Compilation = CSharpCompilation.Create(
            project,
            [.. trees, CSharpSyntaxTree.ParseText(ImplicitUsings, path: "ImplicitUsings.g.cs")],
            References(project),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>The project's name, which is its directory under <c>src/</c>.</summary>
    public string Project { get; }

    /// <summary>The project's own source files, each with its path relative to the repository.</summary>
    public IReadOnlyList<SyntaxTree> Trees { get; }

    /// <summary>The project compiled from its sources.</summary>
    public CSharpCompilation Compilation { get; }

    /// <summary>The sources of one project under <c>src/</c>, read once per suite run.</summary>
    public static SourceCode Of(string project) => Projects.GetOrAdd(project, name => new SourceCode(name));

    /// <summary>The symbol a runtime type stands for in this compilation, which must exist.</summary>
    public INamedTypeSymbol Type(Type type) => Type(type.FullName ?? type.Name);

    /// <summary>The symbol a metadata name stands for in this compilation, which must exist.</summary>
    public INamedTypeSymbol Type(string metadataName)
    {
        INamedTypeSymbol? symbol = Compilation.GetTypeByMetadataName(metadataName);
        Assert.True(symbol is not null, $"{Project} does not know '{metadataName}', so a law naming it would check nothing.");
        return symbol!;
    }

    /// <summary>The members of a type with one name, which must exist.</summary>
    public IReadOnlyList<ISymbol> Members(Type type, string name)
    {
        ISymbol[] members = [.. Type(type).GetMembers(name)];
        Assert.True(members.Length > 0, $"{type.Name} has no member '{name}', so a law naming it would check nothing.");
        return members;
    }

    /// <summary>The semantic model of one of this project's files.</summary>
    public SemanticModel Model(SyntaxTree tree)
    {
        lock (_models)
        {
            if (!_models.TryGetValue(tree, out SemanticModel? model))
            {
                model = Compilation.GetSemanticModel(tree);
                _models.Add(tree, model);
            }

            return model;
        }
    }

    /// <summary>
    /// Every place an instance of a type is constructed: <c>new T(...)</c>, a target-typed <c>new(...)</c> whose
    /// target is the type, and a collection expression or <c>with</c> is not a construction of a class.
    /// </summary>
    public IReadOnlyList<SourceSite> Creations(Type type) => Creations(Type(type));

    /// <summary>Every place an instance of a type symbol is constructed, however the <c>new</c> is spelled.</summary>
    public IReadOnlyList<SourceSite> Creations(INamedTypeSymbol type)
    {
        List<SourceSite> sites = [];
        foreach (SyntaxTree tree in Trees)
        {
            SemanticModel model = Model(tree);
            foreach (BaseObjectCreationExpressionSyntax creation in tree.GetRoot().DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
            {
                ITypeSymbol? created = model.GetTypeInfo(creation).Type;
                if (created is not null && SymbolEqualityComparer.Default.Equals(created.OriginalDefinition, type.OriginalDefinition))
                {
                    sites.Add(SourceSite.At(creation));
                }
            }
        }

        return sites;
    }

    /// <summary>Every name in the project's code that binds to one of the given symbols.</summary>
    /// <remarks>
    /// A member is matched through its original definition, so a generic method's every instantiation is one use,
    /// and an override or an interface implementation is not a use of the member it implements.
    /// </remarks>
    public IReadOnlyList<SourceSite> Uses(params ISymbol[] symbols)
    {
        HashSet<ISymbol> targets = new(symbols.Select(symbol => symbol.OriginalDefinition), SymbolEqualityComparer.Default);
        List<SourceSite> sites = [];
        foreach (SyntaxTree tree in Trees)
        {
            SemanticModel model = Model(tree);
            foreach (SimpleNameSyntax name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (IsDeclarationName(name)) continue;
                if (Bound(model, name).Any(symbol => targets.Contains(symbol.OriginalDefinition)))
                {
                    sites.Add(SourceSite.At(name));
                }
            }
        }

        return sites;
    }

    /// <summary>
    /// Every place the code reaches a type: its name, one of its members, or a target-typed construction of it.
    /// </summary>
    public IReadOnlyList<SourceSite> UsesOfType(Type type) => UsesOfType(Type(type));

    /// <summary>Every place the code reaches a type symbol: its name, one of its members, or a construction of it.</summary>
    public IReadOnlyList<SourceSite> UsesOfType(INamedTypeSymbol type)
    {
        List<SourceSite> sites = [];
        foreach (SyntaxTree tree in Trees)
        {
            SemanticModel model = Model(tree);
            foreach (SyntaxNode node in tree.GetRoot().DescendantNodes())
            {
                bool reaches = node switch
                {
                    SimpleNameSyntax name when !IsDeclarationName(name) =>
                        Bound(model, name).Any(symbol => Reaches(symbol, type)),
                    ImplicitObjectCreationExpressionSyntax creation =>
                        model.GetTypeInfo(creation).Type is { } created && Same(created, type),
                    _ => false,
                };
                if (reaches) sites.Add(SourceSite.At(node));
            }
        }

        return sites;
    }

    /// <summary>Every place the code reaches any type declared in a namespace, or a namespace below it.</summary>
    public IReadOnlyList<SourceSite> UsesOfNamespace(string ns)
    {
        List<SourceSite> sites = [];
        foreach (SyntaxTree tree in Trees)
        {
            SemanticModel model = Model(tree);
            foreach (SimpleNameSyntax name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (IsDeclarationName(name) || name.Ancestors().Any(ancestor => ancestor is UsingDirectiveSyntax or BaseNamespaceDeclarationSyntax)) continue;
                foreach (ISymbol symbol in Bound(model, name))
                {
                    INamedTypeSymbol? owner = symbol as INamedTypeSymbol ?? symbol.ContainingType;
                    string? declared = owner?.ContainingNamespace?.ToDisplayString();
                    if (declared is not null && (declared == ns || declared.StartsWith(ns + ".", StringComparison.Ordinal)))
                    {
                        sites.Add(SourceSite.At(name));
                        break;
                    }
                }
            }
        }

        return sites;
    }

    /// <summary>The declarations of every type in the project, by the file that declares them.</summary>
    public IEnumerable<(SyntaxTree Tree, BaseTypeDeclarationSyntax Declaration, INamedTypeSymbol Symbol)> TypeDeclarations()
    {
        foreach (SyntaxTree tree in Trees)
        {
            SemanticModel model = Model(tree);
            foreach (BaseTypeDeclarationSyntax declaration in tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(declaration) is INamedTypeSymbol symbol) yield return (tree, declaration, symbol);
            }
        }
    }

    /// <summary>
    /// A file's code with every comment and documentation comment removed, for the few laws that read words rather
    /// than symbols — a vocabulary is text wherever it appears in code, including string literals and names.
    /// </summary>
    public static string CodeWithoutComments(SyntaxTree tree)
    {
        System.Text.StringBuilder code = new();
        foreach (SyntaxToken token in tree.GetRoot().DescendantTokens(descendIntoTrivia: false))
        {
            code.Append(' ').Append(token.Text);
        }

        return code.ToString();
    }

    private static bool IsDeclarationName(SimpleNameSyntax name) =>
        name.Parent is NameColonSyntax or NameEqualsSyntax;

    private static IEnumerable<ISymbol> Bound(SemanticModel model, SyntaxNode node)
    {
        SymbolInfo info = model.GetSymbolInfo(node);
        if (info.Symbol is { } symbol)
        {
            yield return symbol;
            yield break;
        }

        foreach (ISymbol candidate in info.CandidateSymbols) yield return candidate;
    }

    private static bool Reaches(ISymbol symbol, INamedTypeSymbol type)
    {
        if (symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } constructor) return Same(constructor.ContainingType, type);
        if (symbol is ITypeSymbol named) return Same(named, type);
        return symbol.ContainingType is { } owner && Same(owner, type);
    }

    private static bool Same(ITypeSymbol candidate, INamedTypeSymbol type) =>
        SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, type.OriginalDefinition);

    /// <summary>
    /// The assemblies a project's own sources compile against: the framework and everything this suite loaded,
    /// less the project's own built assembly, whose types the sources declare.
    /// </summary>
    private static List<MetadataReference> References(string project)
    {
        Dictionary<string, string> byName = new(StringComparer.OrdinalIgnoreCase);
        string trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        foreach (string path in trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        foreach (string path in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
        {
            byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        byName.Remove(project);
        List<MetadataReference> references = [];
        foreach (string path in byName.Values)
        {
            try
            {
                _ = AssemblyName.GetAssemblyName(path);
                references.Add(MetadataReference.CreateFromFile(path));
            }
            catch (BadImageFormatException)
            {
                // A native library beside the managed ones is not a reference.
            }
        }

        return references;
    }
}

/// <summary>One place in a project's source, named the way a failing law reports it.</summary>
/// <param name="File">The file, relative to the repository.</param>
/// <param name="Line">The line, counted from one.</param>
/// <param name="Text">The code at that place, on one line.</param>
public sealed record SourceSite(string File, int Line, string Text)
{
    /// <summary>The site of a node.</summary>
    public static SourceSite At(SyntaxNode node)
    {
        FileLinePositionSpan span = node.GetLocation().GetLineSpan();
        string text = node.ToString().ReplaceLineEndings(" ");
        return new SourceSite(span.Path, span.StartLinePosition.Line + 1, text.Length > 120 ? text[..120] : text);
    }

    /// <inheritdoc />
    public override string ToString() => $"{File}:{Line}: {Text}";
}
