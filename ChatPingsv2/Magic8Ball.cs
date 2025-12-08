public static class Magic8Ball
{
    static readonly string[] Responses = new string[]
    {
        "It is certain",
        "Without a doubt",
        "You may rely on it",
        "Yes definitely",
        "Most likely",
        "Outlook good",
        "Yes",
        "Signs point to yes",
        "Reply hazy try again",
        "Ask again later",
        "Better not tell you now",
        "Cannot predict now",
        "Concentrate and ask again",
        "Don't count on it",
        "My reply is no",
        "Outlook not so good",
        "Very doubtful"
    };

    static readonly Random rng = new Random();

    public static string Ask(string question)
    {
        if (string.IsNullOrWhiteSpace(question) || !question.EndsWith('?'))
            return "You must ask a question";
        int index = rng.Next(Responses.Length);
        return Responses[index];
    }
}