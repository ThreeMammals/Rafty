namespace Rafty.IntegrationTests
{
    public class FakeCommand
    {
        public FakeCommand(string value)
        {
            this.Value = value;

        }
        public string Value { get; private set; }
    }
}
