namespace Rafty.Infrastructure
{

    public abstract class Response<T>
    {
        public Response(T command)
        {
            Command = command;
        }

        public T Command {get;private set;}
    }
}