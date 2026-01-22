namespace DataFlow.POC.Core;

/// <summary>
/// Provides access to parameters from trigger context in a decoupled manner.
/// Abstracts away specific trigger context types, allowing actors to request
/// parameters by name regardless of the trigger source.
/// </summary>
public interface IParameterProvider
{
    /// <summary>
    /// Attempts to get a parameter value by name.
    /// </summary>
    /// <typeparam name="T">The expected type of the parameter value.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <param name="value">The parameter value if found; otherwise, default(T).</param>
    /// <returns>True if the parameter was found and successfully converted; otherwise, false.</returns>
    bool TryGetParameter<T>(string name, out T? value);
    
    /// <summary>
    /// Gets a required parameter value by name.
    /// </summary>
    /// <typeparam name="T">The expected type of the parameter value.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <returns>The parameter value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the parameter is not found or cannot be converted.</exception>
    T GetRequiredParameter<T>(string name);
    
    /// <summary>
    /// Gets a parameter value by name with a default value if not found.
    /// </summary>
    /// <typeparam name="T">The expected type of the parameter value.</typeparam>
    /// <param name="name">The parameter name.</param>
    /// <param name="defaultValue">The default value to return if the parameter is not found.</param>
    /// <returns>The parameter value if found; otherwise, the default value.</returns>
    T GetParameter<T>(string name, T defaultValue);
}
