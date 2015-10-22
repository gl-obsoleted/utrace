/*!lic_info

The MIT License (MIT)

Copyright (c) 2015 SeaSunOpenSource

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

*/

﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
 

namespace utrace
{
    public class NetConfig
    {
        public const int BufferSize = 1024;
    }

    public class NetClient : IDisposable
    {
        public event SysPost.StdMulticastDelegation Connected;
        public event SysPost.StdMulticastDelegation Disconnected;
        public event SysPost.StdMulticastDelegation Prompted;

        public bool IsConnected { get { return _tcpClient != null; } }

        public string RemoteAddr { get { return IsConnected ? _tcpClient.Client.RemoteEndPoint.ToString() : ""; } }

        public void Connect(string host, int port)
        {
            _host = host;
            _port = port;
            _tcpClient = new TcpClient();
            _tcpClient.BeginConnect(_host, _port, OnConnect, _tcpClient);
            UsLogging.Printf(LogWndOpt.Bold, "connecting to [u]{0}:{1}[/u]...", host, port);
        }

        public void Disconnect()
        {
            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient = null;

                _host = "";
                _port = 0;

                UsLogging.Printf("connection closed.");
                SysPost.InvokeMulticast(this, Disconnected);
            }
        }

        public void RegisterCmdHandler(eNetCmd cmd, UsCmdHandler handler)
        {
            _cmdParser.RegisterHandler(cmd, handler);
        }

        public void Tick_CheckConnectionStatus()
        {
            try
            {
                if (!_tcpClient.Connected)
                {
                    UsLogging.Printf("disconnection detected. (_tcpClient.Connected == false).");
                    throw new Exception();
                }

                // check if the client socket is still readable
                if (_tcpClient.Client.Poll(0, SelectMode.SelectRead))
                {
                    byte[] checkConn = new byte[1];
                    if (_tcpClient.Client.Receive(checkConn, SocketFlags.Peek) == 0)
                    {
                        UsLogging.Printf("disconnection detected. (failed to read by Poll/Receive).");
                        throw new IOException();
                    }
                }
            }
            catch (Exception ex)
            {
                DisconnectOnError("disconnection detected while checking connection status.", ex);
            }
        }

        private StringBuilder _receivedBuffer = new StringBuilder();

        private bool hasPromptInTheEnd(StringBuilder builder)
        {
            if (builder.Length < 2)
                return false;

            return builder[builder.Length - 1] == ' ' && builder[builder.Length - 2] == '>';
        }

        public void Tick_ReceivingData()
        {
            try
            {
                byte[] buf = new byte[NetConfig.BufferSize];
                while (_tcpClient.Available > 0)
                {
                    int read = _tcpClient.GetStream().Read(buf, 0, buf.Length);
                    if (read > 0)
                    {
                        _receivedBuffer.Append(Encoding.Default.GetString(buf, 0, read));
                    }
                }

                if (_receivedBuffer.Length > 0)
                {
                    string reply = _receivedBuffer.ToString();
                    if (reply.Length > 0)
                    {
                        // by replacing these [], we ensure that BBCode parsing wouldn't be 
                        // interupted by unintentional BBCode-unawared characters
                        UsLogging.Printf(reply.Replace('[', '<').Replace(']', '>'));
                    }

                    if (hasPromptInTheEnd(_receivedBuffer))
                        SysPost.InvokeMulticast(this, Prompted);

                    _receivedBuffer.Clear();
                }
            }
            catch (Exception ex)
            {
                DisconnectOnError("error detected while receiving data.", ex);
            }
        }

        public void Dispose()
        {
            Disconnect();
        }

        public void SendPacket(UsCmd cmd)
        {
            UsLogging.Printf("SendPacket() is not available for this tool, use SendText() instead.");
        }

        public void SendText(string content)
        {
            try
            {
                byte[] bytes = Encoding.Default.GetBytes(content);
                _tcpClient.GetStream().Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                DisconnectOnError("error detected while sending text data.", ex);
            }
        }

        // Called when a connection to a server is established
        private void OnConnect(IAsyncResult asyncResult)
        {
            // Retrieving TcpClient from IAsyncResult
            TcpClient tcpClient = (TcpClient)asyncResult.AsyncState;

            try
            {
                if (tcpClient.Connected) // may throw NullReference
                {
                    UsLogging.Printf("connected successfully.");
                    SysPost.InvokeMulticast(this, Connected);
                }
                else
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                DisconnectOnError("connection failed while handling OnConnect().", ex);
            }
        }

        private void DisconnectOnError(string info, Exception ex)
        {
            UsLogging.Printf(LogWndOpt.Bold, info);
            UsLogging.Printf(ex.ToString());

            Disconnect();
        }

        private string _host = "";
        private int _port = 0;
        private TcpClient _tcpClient;
        private UsCmdParsing _cmdParser = new UsCmdParsing();
    }
}
