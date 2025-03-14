﻿using FastGithub.DomainResolve;
using Microsoft.AspNetCore.Connections;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace FastGithub.ReverseProxy
{
    /// <summary>
    /// github的ssh代理处理者
    /// </summary>
    sealed class GithubSshProxyHandler : ConnectionHandler
    {
        private readonly IDomainResolver domainResolver;
        private readonly DnsEndPoint githubSshEndPoint = new("ssh.github.com", 443);

        /// <summary>
        /// github的ssh代理处理者
        /// </summary>
        /// <param name="domainResolver"></param>
        public GithubSshProxyHandler(IDomainResolver domainResolver)
        {
            this.domainResolver = domainResolver;
        }

        /// <summary>
        /// ssh连接后
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var address = await this.domainResolver.ResolveAsync(this.githubSshEndPoint);
            using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            await socket.ConnectAsync(address, this.githubSshEndPoint.Port);
            var targetStream = new NetworkStream(socket, ownsSocket: false);

            var task1 = targetStream.CopyToAsync(connection.Transport.Output);
            var task2 = connection.Transport.Input.CopyToAsync(targetStream);
            await Task.WhenAny(task1, task2);
        }
    }
}
