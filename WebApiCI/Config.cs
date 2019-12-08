using System;
using System.Collections.Generic;
using System.Text;

namespace WebApiCI
{
    public class Config
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string RemoteUrl { get; set; }
        public string WorkDir { get; set; }
        public int ListenerPort { get; set; }

        public List<CommandConfig> Cmds { get; set; }
    }

    public class CommandConfig
    {
        public string FileName { get; set; }
        public string WorkSpace { get; set; }
        public string Command { get; set; }
    }
}
