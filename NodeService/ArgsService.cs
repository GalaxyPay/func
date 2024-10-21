namespace NodeService
{
    public class ArgsService(string[] args)
    {
        private readonly string[] _args = args;

        public string[] GetArgs()
        {
            return _args;
        }
    }
}
