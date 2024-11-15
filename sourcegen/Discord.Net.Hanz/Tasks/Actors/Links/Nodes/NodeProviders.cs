using System.Collections.Immutable;
using System.Text;
using Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes.Common;
using Discord.Net.Hanz.Tasks.Actors.V3;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Discord.Net.Hanz.Tasks.Actors.Links.V5.Nodes;

public readonly struct Source : IEquatable<Source>
{
    public readonly string Name;
    public readonly SourceText Content;

    public Source(string name, string content)
    {
        Name = name;
        Content = SourceText.From(content, Encoding.UTF8);
    }

    public Source(string name, SourceText content)
    {
        Name = name;
        Content = content;
    }

    public bool Equals(Source other)
    {
        return Name == other.Name && Content.ContentEquals(other.Content);
    }

    public override bool Equals(object? obj)
    {
        return obj is Source other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Name.GetHashCode() * 397) ^ HashCode.OfEach(Content.GetChecksum());
        }
    }

    public static bool operator ==(Source left, Source right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Source left, Source right)
    {
        return !left.Equals(right);
    }
}

public readonly struct NodeProviders
{
    public readonly record struct Hierarchy(
        string Actor,
        ImmutableEquatableArray<ActorInfo> Parents,
        ImmutableEquatableArray<ActorInfo> Children
    );

    public IncrementalValueProvider<Grouping<string, ActorInfo>> ActorAncestors { get; }

    public IncrementalValuesProvider<Hierarchy> ActorHierarchy { get; }

    public IncrementalValueProvider<Keyed<string, ActorInfo>> KeyedActorInfo { get; }
    
    public IncrementalValuesProvider<ActorInfo> ActorInfos { get; }

    public IncrementalValuesProvider<LinkSchematics.Schematic> Schematics { get; }

    public IncrementalValuesProvider<ActorsTask.ActorSymbols> Actors { get; }

    public IncrementalValuesProvider<LinksV5.NodeContext> Contexts { get; }

    private readonly IncrementalGeneratorInitializationContext _context;

    public NodeProviders(
        IncrementalValuesProvider<LinkSchematics.Schematic> schematics,
        IncrementalValuesProvider<ActorsTask.ActorSymbols> actors,
        IncrementalValuesProvider<LinksV5.NodeContext> contexts,
        IncrementalGeneratorInitializationContext context)
    {
        _context = context;
        Schematics = schematics;
        Actors = actors;
        Contexts = contexts;

        ActorHierarchy = actors
            .Collect()
            .SelectMany(GetHierarchy);

        ActorInfos = actors
            .Select((x, _) => ActorInfo.Create(x));
        
        KeyedActorInfo = ActorInfos.ToKeyed(x => x.Actor.DisplayString);

        ActorAncestors = ActorHierarchy
            .Collect()
            .GroupManyBy(x => x.Actor, x => x.Parents);
    }
    
    public void AddSourceOutput<T>(
        IncrementalValuesProvider<T> provider, 
        Func<T, IEnumerable<Source>> func)
    {
        _context.RegisterSourceOutput(
            provider,
            (context, state) =>
            {
                foreach (var source in func(state))
                {
                    context.AddSource(source.Name, source.Content);
                }
            }
        );
    }
    
    public void AddSourceOutput<T>(
        IncrementalValueProvider<T> provider, 
        Func<T, IEnumerable<Source>> func)
    {
        _context.RegisterSourceOutput(
            provider,
            (context, state) =>
            {
                foreach (var source in func(state))
                {
                    context.AddSource(source.Name, source.Content);
                }
            }
        );
    }

    private static IEnumerable<Hierarchy> GetHierarchy(
        ImmutableArray<ActorsTask.ActorSymbols> targets,
        CancellationToken token)
    {
        foreach (var target in targets)
        {
            yield return new Hierarchy(
                target.Actor.ToDisplayString(),
                targets
                    .Where(x => Net.Hanz.Hierarchy.Implements(target.Actor, x.Actor))
                    .Select((x, _) => ActorInfo.Create(x))
                    .ToImmutableEquatableArray(),
                targets
                    .Where(x => Net.Hanz.Hierarchy.Implements(x.Actor, target.Actor))
                    .Select((x, _) => ActorInfo.Create(x))
                    .ToImmutableEquatableArray()
            );

            token.ThrowIfCancellationRequested();
        }
    }
}