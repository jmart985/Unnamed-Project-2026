namespace Simulation;

// Keep framework-independent validation semantics identical on both build targets.
internal static class CompatibilityGuard
{
    public static void NotBlank(string? value, string parameterName)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("The value cannot be empty or whitespace.", parameterName);
    }

    public static bool IsDefined<T>(T value) where T : struct, Enum
        => Enum.IsDefined(typeof(T), value);
}
