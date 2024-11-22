using System.Diagnostics;
using System.Reflection;
using Discord.Net.Hanz.Nodes;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz;

public abstract class GenerationTask
{
    private static readonly Dictionary<Type, GenerationTask> _tasks = [];

    private static readonly Logger _logger = Logger.CreateForTask("GenerationTaskBuilder").WithCleanLogFile();

    protected Logger Logger { get; }
    
    public GenerationTask(IncrementalGeneratorInitializationContext context, Logger logger)
    {
        Logger = logger;
    }

    public static void Initialize(IncrementalGeneratorInitializationContext context)
    {
        _tasks.Clear();
        _logger.Clean();

        try
        {
            var queue = new Queue<Type>(
                typeof(GenerationTask).Assembly.GetTypes()
                    .Where(x => !x.IsAbstract && typeof(GenerationTask).IsAssignableFrom(x))
            );

            _logger.Log($"{queue.Count} tasks to initialize...");

            while (queue.Count > 0)
            {
                var type = queue.Dequeue();

                GetOrCreate(type, context);
            }
        }
        catch (Exception x)
        {
            _logger.Log($"Failed: {x}");
        }
        finally
        {
            _logger.Flush();
        }
    }

    public T GetTask<T>(IncrementalGeneratorInitializationContext context) where T : GenerationTask
        => GetOrCreate<T>(context);

    private static T GetOrCreate<T>(IncrementalGeneratorInitializationContext context) where T : GenerationTask
        => (T)GetOrCreate(typeof(T), context);

    private static GenerationTask GetOrCreate(Type type, IncrementalGeneratorInitializationContext context)
    {
        lock (_tasks)
        {
            if (_tasks.TryGetValue(type, out var rawTask))
                return rawTask;
        
            var logger = 
                typeof(Node).IsAssignableFrom(type)
                    ? Node.NodeLogger.GetSubLogger(type.Name)
                    : Logger.CreateForTask(type.Name).WithCleanLogFile();
        
            _logger.Log($"Creating instance of {type}..");
        
            var instance = (GenerationTask) Activator.CreateInstance(type, context, logger);
            _tasks[type] = instance;
        
            logger.Flush();
        
            return instance;
        }
    }
}