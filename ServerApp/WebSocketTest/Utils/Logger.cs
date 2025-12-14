using System;

namespace WebSocketTest.Utils
{
    public class Logger
    {
        private readonly Action<string> _log;

        public Logger(Action<string> log)
        {
            _log = log ?? (_ => { });
        }

        public void Log(string msg) => _log(msg);
    }
}
