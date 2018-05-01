namespace Rafty.Concensus
{
    public class ErrorResponse<T> : Response<T>
    {
        public ErrorResponse(string error, T command) 
            : base(command)
        {
            Error = error;
        }

        public string Error { get; private set; }
    }
}