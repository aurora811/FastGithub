﻿using FastGithub.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FastGithub.DomainResolve
{
    /// <summary>
    /// DnscryptProxy服务
    /// </summary>
    sealed class DnscryptProxy
    {
        private const string PATH = "dnscryptproxy";
        private const string NAME = "dnscrypt-proxy";

        /// <summary>
        /// 相关进程
        /// </summary>
        private Process? process;

        /// <summary>
        /// 获取监听的节点
        /// </summary>
        public IPEndPoint? LocalEndPoint { get; private set; }

        /// <summary>
        /// 启动dnscrypt-proxy
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var tomlPath = Path.Combine(PATH, $"{NAME}.toml");
            var port = LocalMachine.GetAvailablePort(IPAddress.Loopback.AddressFamily, min: 5353);
            var localEndPoint = new IPEndPoint(IPAddress.Loopback, port);

            await TomlUtil.SetListensAsync(tomlPath, localEndPoint, cancellationToken);
            await TomlUtil.SetEdnsClientSubnetAsync(tomlPath, cancellationToken);

            foreach (var process in Process.GetProcessesByName(NAME))
            {
                process.Kill();
                process.WaitForExit();
            }

            if (OperatingSystem.IsWindows())
            {
                StartDnscryptProxy("-service uninstall")?.WaitForExit();
                StartDnscryptProxy("-service install")?.WaitForExit();
                StartDnscryptProxy("-service start")?.WaitForExit();
                this.process = Process.GetProcessesByName(NAME).FirstOrDefault(item => item.SessionId == 0);
            }
            else
            {
                this.process = StartDnscryptProxy(string.Empty);
            }

            if (this.process != null)
            {
                this.LocalEndPoint = localEndPoint;
                this.process.EnableRaisingEvents = true;
                this.process.Exited += Process_Exited;
            }
        }

        /// <summary>
        /// 进程退出时
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Process_Exited(object? sender, EventArgs e)
        {
            this.LocalEndPoint = null;
        }

        /// <summary>
        /// 停止dnscrypt-proxy
        /// </summary>
        public void Stop()
        {
            if (OperatingSystem.IsWindows())
            {
                StartDnscryptProxy("-service stop")?.WaitForExit();
                StartDnscryptProxy("-service uninstall")?.WaitForExit();
            }

            if (this.process != null && this.process.HasExited == false)
            {
                this.process.Kill();
            }
            this.LocalEndPoint = null;
        }

        /// <summary>
        /// 启动DnscryptProxy进程
        /// </summary>
        /// <param name="arguments"></param> 
        private static Process? StartDnscryptProxy(string arguments)
        {
            var fileName = OperatingSystem.IsWindows() ? $"{NAME}.exe" : NAME;
            return Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(PATH, fileName),
                Arguments = arguments,
                WorkingDirectory = PATH,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        /// <summary>
        /// 转换为字符串
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return NAME;
        }
    }
}
