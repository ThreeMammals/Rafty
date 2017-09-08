namespace Rafty.Concensus
{
    public class Response<T>
    {
        public Response(bool success, T command)
        {
            Success = success;
            Command = command;
        }

        public bool Success {get;private set;}
        public T Command {get;private set;}
    }
}