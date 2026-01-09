using System.Text.Json;
using MassTransit;
using Sdk.Messaging;

namespace ClusterManagement.Backend.Extensions;

internal static class ExecuteContextExtensions
{
    /// <summary>
    /// serializes the item and adds it to the variables dictionary as key:nameof(T), value:string
    /// </summary>
    /// <param name="context"></param>
    /// <param name="item"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ExecutionResult CompletedWithObject<T>(this ExecuteContext context, T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        var key = item.GetType().Name;
        var serialized = JsonSerializer.Serialize(item, DefaultJsonSerializerSettings.Default);

        return context.CompletedWithVariables([new(key, serialized)]);
    }

    /// <summary>
    /// serializes the item and adds it to the variables dictionary as key:nameof(T), value:string
    /// </summary>
    /// <param name="context"></param>
    /// <param name="exception"></param>
    /// <param name="item"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static ExecutionResult FaultedWithObject<T>(this ExecuteContext context, Exception exception, T item)
    {
        if (item is null)
            throw new ArgumentNullException(nameof(item));

        var key = item.GetType().Name;
        var serialized = JsonSerializer.Serialize(item, DefaultJsonSerializerSettings.Default);

        return context.FaultedWithVariables(exception, [new(key, serialized)]);
    }
}
