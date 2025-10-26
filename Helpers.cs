public static class Helpers
{
    /// <summary>
    /// Takes a name and checks if it's acceptable to use.
    /// </summary>
    /// <param name="name">Input name</param>
    /// <returns>Return validated name</returns>
    /// <exception cref="Exception"></exception>
    public static string ValidateName(string name)
    {
        if (name.Length > 32)
        {
            name = name.Substring(0, 32);
        }
        return name;
    }

    /// <summary>
    /// Takes a message and checks if it's acceptable to use.
    /// </summary>
    /// <param name="text">Input text</param>
    /// <returns>Return validated message</returns>
    /// <exception cref="ArgumentException"></exception>
    public static string ValidateMessage(string message)
    {
        if (message.Length > 256)
        {
            message = message.Substring(0, 256);
        }
        return message;
    }
}