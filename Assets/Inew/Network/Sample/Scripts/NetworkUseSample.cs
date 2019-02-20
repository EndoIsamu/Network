using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Inew.Network
{
    public class NetworkUseSample : MonoBehaviour
    {
        // component
        public Text receivedTextComponent;
        public Text netEventTextComponent;
        public Text roleTextComponent;
        public InputField inputComponent;
        public string ipAddress = "127.0.0.1";
        public int port = 8888;

        // server
        private TcpServer _server;

        // client
        private const int BUFFER_SIZE = 1400;
        private TcpClient _client;
        private NetworkStream _clientStream;
        private bool _connectionLoop = false;

        // others
        private static SynchronizationContext _mainContext;
        private System.Text.Encoding _encoding;
        private bool _initialized = false;
        private bool _isServer = false;

        void Awake()
        {
            _server = new TcpServer();
            _client = new TcpClient();

            _server.OnDataReceived += OnDataReceived;
            _server.NetEventHandler += OnNetStateChenged;

            inputComponent.onEndEdit.AddListener(SendCommunication);

            _mainContext = SynchronizationContext.Current;
            _encoding = System.Text.Encoding.UTF8;
        }

        private async void Update()
        {
            await UpdateInput();
        }

        private void OnDisable()
        {
            if (_isServer)
            {
                _server.DisConnectAll();
            }
            else
            {
                _connectionLoop = false;
            }
        }

        private async Task UpdateInput()
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                await _client.ConnectAsync(IPAddress.Parse(ipAddress), port);

                _clientStream = _client.GetStream();

                //タイムアウト設定
                _clientStream.ReadTimeout = 10000;
                _clientStream.WriteTimeout = 10000;

                StartClientReceiveCommunication(OnDataReceived);

                roleTextComponent.text = "Client";
            }

            if (Input.GetKeyDown(KeyCode.S))
            {
                _server.Launch(port);
                _server.ListenAsync();
                _server.CommunicateAsync();

                _isServer = true;
                roleTextComponent.text = "Server";
            }

            _initialized = true;
        }

        private void StartClientReceiveCommunication(Action<string> callback)
        {
            _connectionLoop = true;

            Task.Run(async () => 
            {
                while (_connectionLoop)
                {
                    var memoryStream = new MemoryStream();

                    while (_client.Available > 0)
                    {
                        // データがある場合は限界まで読む
                        var buffer = new byte[BUFFER_SIZE];
                        var size = await _clientStream.ReadAsync(buffer, 0, BUFFER_SIZE);

                        if (size > 0)
                        {
                            Debug.Log(_encoding.GetString(buffer, 0, BUFFER_SIZE));
                            await memoryStream.WriteAsync(buffer, 0, BUFFER_SIZE);
                        }
                    }

                    var messageSize = (int)memoryStream.Length;
                    if (messageSize > 0)
                    {
                        var message = _encoding.GetString(memoryStream.GetBuffer(), 0, messageSize);
                        memoryStream.Close();

                        _mainContext.Post(_ => callback(message), null);
                    }
                }
            });
        }

        private void OnDataReceived(string message)
        {
            receivedTextComponent.text = message;
        }

        private void OnNetStateChenged(NetEventState state)
        {
            var text = $"[Server] Type : {state.type}, Result : {state.result}";

            netEventTextComponent.text = text;
            Debug.Log(text);
        }

        private async void SendCommunication(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            //データを送信する

            if (_isServer)
            {
                _server.Send(message, 0);
            }
            else
            {
                //文字列をByte型配列に変換
                byte[] sendBytes = _encoding.GetBytes(message + '\n');
                //データを送信する
                await _clientStream.WriteAsync(sendBytes, 0, sendBytes.Length);
            }

            inputComponent.text = "";
            inputComponent.ActivateInputField();

        }
    }
}
