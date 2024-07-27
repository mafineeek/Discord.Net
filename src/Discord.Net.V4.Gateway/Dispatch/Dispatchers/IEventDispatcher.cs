using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Discord.Gateway;

public interface IEventDispatcher
{
    ValueTask DispatchAsync<T>(string eventName, HashSet<T> handlers, CancellationToken token)
        where T : ITransientDispatchHandler;
}
