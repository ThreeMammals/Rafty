namespace Rafty.Concensus
{
    public class Response<T>
    {
        public Response(bool success, T command)
        {
            Success = success;
            Command = command;
        }

        public Response(string error, T command)
        {
            Error = error;
            Success = false;
            Command = command;
        }

        public string Error {get;private set;}
        public bool Success {get;private set;}
        public T Command {get;private set;}
    }
}