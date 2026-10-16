namespace M3UAnalyzer.Domain.Sources;

/// <summary>Xtream connection details. <see cref="ToString"/> never prints the username or password.</summary>
public sealed record XtreamCredentials(string BaseUrl, string Username, string Password)
{
    public override string ToString() => $"XtreamCredentials {{ BaseUrl = {BaseUrl}, Username = ***, Password = *** }}";
}
