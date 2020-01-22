using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WebApiCI
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool GenerateConsoleCtrlEvent(int dwCtrlEvent, int dwProcessGroupId);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleCtrlHandler(IntPtr handlerRoutine, bool add);

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        private static Process process;
        private static bool hasNew = false;

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder();
            IConfiguration config = builder.SetBasePath(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
                .AddJsonFile("config.json", true, true)
                .Build();
            var myConfig = config.Get<Config>();

            Console.WriteLine("Hello World!");
           
            var userName = myConfig.UserName;
            char[] pChar = myConfig.Password.ToCharArray();

            SecureString password = new SecureString();
            foreach (var item in pChar)
            {
                password.AppendChar(item);
            }
            CredentialsHandler credentialsProvider = (_url, _user, _cred) => new SecureUsernamePasswordCredentials()
            {
                Username = userName,
                Password = password
            };
            var workpath = myConfig.WorkDir;
            var logMessage = "";


            Task.Factory.StartNew(() =>
            {
                TcpListener listener = new TcpListener(IPAddress.Parse("0.0.0.0"), myConfig.ListenerPort);
                listener.Start();
                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    
                    client.Close();
                    hasNew = true;
                    try
                    {
                        Console.WriteLine("close！");
                        AttachConsole(process.Id);
                        SetConsoleCtrlHandler(IntPtr.Zero, true);   //设置自己的ctrl+c处理，防止自己被终止
                        GenerateConsoleCtrlEvent(0, 0); // 发送ctrl+c（注意：这是向所有共享该console的进程发送）
                        Thread.Sleep(10);
                        SetConsoleCtrlHandler(IntPtr.Zero, false);  //重置此参数
                    }
                    catch (Exception ex)
                    {
                        //Console.WriteLine(ex.ToString());
                    }
                }
            });


          


            while (true)
            {
                while (!hasNew)
                {
                    Thread.Sleep(1000);
                }
                hasNew = false;
                Console.WriteLine("有新更新！");

                if (!Directory.Exists(workpath) || Directory.GetFiles(workpath).Length == 0)
                {
                    Repository.Clone(myConfig.RemoteUrl, workpath, new CloneOptions
                    {
                        CredentialsProvider = credentialsProvider
                        ,
                        OnProgress = OnProgress
                    }); ;
                }
                else
                {
                    using (var repo = new Repository(workpath))
                    {
                        FetchOptions options = new FetchOptions();
                        options.CredentialsProvider = credentialsProvider;
                        options.OnProgress = OnProgress;
                        foreach (Remote remote in repo.Network.Remotes)
                        {
                            IEnumerable<string> refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                            //Commands.Fetch(repo, remote.Name, refSpecs, options, logMessage);
                            Commands.Pull(repo, new Signature(userName, userName, new DateTimeOffset(DateTime.Now)), new PullOptions { FetchOptions = options });
                        }
                    }
                }

                Console.WriteLine(logMessage);
                //Excute("dotnet", "restore", $@"{workpath}\Api\WebApi", out process);
                //Excute("dotnet", "run", $@"{workpath}\Api\WebApi\WebApi", out process);

                foreach (var cmd in myConfig.Cmds)
                {
                    Excute(cmd.FileName, cmd.Command, cmd.WorkSpace.Replace("{WorkDir}", myConfig.WorkDir), out process);
                }
                //Console.WriteLine("更新完成！");

            }
        }

        private static bool OnProgress(string msg)
        {
            Console.WriteLine(msg);
            return true;
        }

        private static void Excute(string fileName, string arguments,string workdir ,out Process proc)
        {
            //创建一个ProcessStartInfo对象 使用系统shell 指定命令和参数 设置标准输出
            var startInfo = new ProcessStartInfo(fileName) { RedirectStandardOutput = true,UseShellExecute = false };
            if(!string.IsNullOrEmpty(workdir))
            {
                startInfo.WorkingDirectory = workdir;
            }

            if (!fileName.Equals("cmd"))
            {
                startInfo.Arguments = arguments;
            }
            else
            {
                startInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
            }


            //启动
            proc = Process.Start(startInfo);

            //proc.OutputDataReceived += Proc_OutputDataReceived;

            if (proc == null)
            {
                Console.WriteLine("Can not exec.");
            }
            else
            {
                //向cmd窗口写入命令
                if (fileName.Equals("cmd"))
                {
                    proc.StandardInput.WriteLine(arguments);
                    proc.StandardInput.AutoFlush = true;
                }

                Console.WriteLine("-------------Start read standard output--------------");
                //开始读取
                using (var sr = proc.StandardOutput)
                {
                    while (!sr.EndOfStream)
                    {
                        Console.WriteLine(sr.ReadLine());
                    }

                    if (!proc.HasExited)
                    {
                        Console.WriteLine("---------------Kill------------------");
                        proc.Kill();
                    }
                }
                Console.WriteLine("---------------Read end------------------");

            }
        }

        private static void Proc_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }
    }
}