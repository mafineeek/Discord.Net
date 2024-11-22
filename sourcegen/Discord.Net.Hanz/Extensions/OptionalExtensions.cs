using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz;

public static class OptionalExtensions
{
    public static Optional<T> Some<T>(this T value) => new(value);

    public static Optional<T> AsOptional<T>(this T? value)
        where T : struct
    {
        if (value.HasValue)
            return Some(value.Value);

        return default;
    }

    public static Optional<U> Map<T, U>(this Optional<T> optional, Func<T, U> mapper)
        => optional.HasValue ? mapper(optional.Value).Some() : default; 
    
    public static T Or<T>(this Optional<T> optional, T value) => optional.HasValue ? optional.Value : value;
    public static Optional<T> Or<T>(this Optional<T> optional, Optional<T> value) 
        => optional.HasValue ? optional : value;
}