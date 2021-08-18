namespace VideoCompresser
{
    public struct ConsoleCommand
    {
        public ConsoleCommand(string command, string args)
        {
            Command = command;
            Args = args;
        }

        public string Command { get; set; }
        public string Args { get; set; }
    }
}
