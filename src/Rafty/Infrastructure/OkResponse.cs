namespace Rafty.Concensus
{
    public class OkResponse<T> : Response<T>
    {
        public OkResponse(T command) 
            : base(command)
        {
        }
    }
}