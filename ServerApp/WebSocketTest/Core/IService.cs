namespace WebSocketTest.Core
{
    public interface IService
    {
        // Handle a command and return JSON string
        string Handle(string command, string arg);
    }
}
