namespace Brickwork.Exporters.Foundry;

internal static class FoundryIdGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public static string Create() => Create(16);

    public static string Create(int length)
    {
        Span<char> buffer = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[Random.Shared.Next(Alphabet.Length)];
        }

        return new string(buffer);
    }
}
