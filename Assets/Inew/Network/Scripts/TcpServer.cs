// -----------------------------------------------------------------------
// ログを吐き出したくない場合は以下の「SERVER_LOG」をコメントアウトする
// -----------------------------------------------------------------------
#define SERVER_LOG


using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Inew.Network
{
    class TcpServer
    {
        // サーバのソケット接続上限
        private const int LIMIT_CONNECT = 5;

        // 送受信時のパケットのバッファサイズ
        private const int BUFFER_SIZE = 1400; 

        private TcpListener _listener;
        private List<TcpClientData> _clients;
        private Encoding _encoding;

        // スレッドのループ許可用フラグ
        private bool _listenLoop = false;
        private bool _communicateLoop = false;

        // イベント通知
        public delegate void EventHandler(NetEventState state);
        private EventHandler _handler;

        private object _lockObj = new object();

        /// <summary>
        /// クライアントとの通信に必要なデータセット
        /// </summary>
        public class TcpClientData
        {
            public TcpClient client;
            public NetworkStream stream;
            public StreamReader reader;
            public PacketQueue sendQueue;
            public PacketQueue recvQueue;
            public bool Connected = false;

            public TcpClientData(TcpClient tcpClient)
            {
                client = tcpClient;
            }
        }

        
        public TcpServer(Encoding enc = null)
        {
            _clients = new List<TcpClientData>();

            _encoding = enc ?? Encoding.UTF8;
        }

        /// <summary>
        /// TCPリスナーを立ち上げる
        /// </summary>
        /// <param name="connectLimit"></param>
        public void Launch(int port, int connectLimit = LIMIT_CONNECT)
        {
            _listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
            _listener.Start();

#if SERVER_LOG
            UnityEngine.Debug.Log($"[Server] Launch server.");
#endif
        }

        /// <summary>
        /// Clientからの接続待ちを開始する
        /// </summary>
        public void ListenAsync()
        {
            _listenLoop = true;
            var task = Task.Run(() => Listen());

#if SERVER_LOG
            UnityEngine.Debug.Log($"[Server] Start listening server.");
#endif
        }

        /// <summary>
        /// 非同期で受信・送信処理を開始する
        /// </summary>
        public void CommunicateAsync()
        {
            _communicateLoop = true;
            var task = Task.Run(() => Communicate());
        }

        /// <summary>
        /// クライアントからの接続待ちをする処理
        /// </summary>
        /// <returns></returns>
        private async Task Listen()
        {
            while (_listenLoop)
            {
                try
                {
                    // クライアントからの接続を受け入れる
                    var client = await _listener.AcceptTcpClientAsync();

                    // 接続されていないなら再び待つ
                    if(client.Connected == false) continue;

                    var data = new TcpClientData(client);
                    data.stream = client.GetStream();
                    data.reader = new StreamReader(data.stream, Encoding.UTF8);
                    data.sendQueue = new PacketQueue();
                    data.recvQueue = new PacketQueue();
                    data.Connected = true;
                    
                    _clients.Add(data);
#if SERVER_LOG
                    UnityEngine.Debug.Log("[Server] Connected from client.");
#endif

                    // 接続完了を通知
                    if (_handler != null) {
                        NetEventState state = new NetEventState();
                        state.type = NetEventType.Connect;
                        state.result = NetEventResult.Success;
                        _handler(state);
                    }
                }
                catch (ObjectDisposedException)
                {
#if SERVER_LOG
                    UnityEngine.Debug.Log("[Server] Listener socket is closed.");
#endif
                    _listenLoop = false;
                }
                catch (Exception e)
                {
#if SERVER_LOG
                    UnityEngine.Debug.LogWarning("[Server] " + e);
#endif
                    throw;
                }
            }
        }

        /// <summary>
        /// 全てのクライアントとの送信・受信処理
        /// </summary>
        /// <remark>
        /// SendCommunicationなどはさらに非同期で並列に回した方がいいかも？
        /// </remark>
        private void Communicate()
        {
            while (_communicateLoop)
            {
                try
                {
                    for (int i = 0; i < _clients.Count; i++)
                    {
                        if (
                            _clients[i] != null && 
                            _clients[i].client.Client.Connected &&
                            _clients[i].client.Client.Poll(1000, SelectMode.SelectRead) && 
                            _clients[i].client.Client.Available == 0
                        ) {
                            // there is no data available to read so connection is not active

                            DisConnect(_clients[i]);
                        }
                        else
                        {
                            SendCommunication(i);

                            RecvCommunication(i);
                        }
                    }
                }
                catch (Exception e)
                {
#if SERVER_LOG
                    UnityEngine.Debug.LogWarning("[Server] " + e);
#endif
                    _communicateLoop = false;
                }
            }
        }

        /// <summary>
        /// スレッドセーフなキューに格納されているメッセージをクライアントに送信します
        /// </summary>
        /// <param name="clientNum"></param>
        private void SendCommunication(int clientNum)
        {
            var client = _clients[clientNum];

            try {
                // 送信処理.
                if (client.Connected)
                {
                    byte[] buffer = new byte[BUFFER_SIZE];

                    int sendSize = client.sendQueue.Dequeue(ref buffer, BUFFER_SIZE);

                    while (sendSize > 0) {
                        client.stream.Write(buffer, 0, sendSize);
                        sendSize = client.sendQueue.Dequeue(ref buffer, BUFFER_SIZE);
                    }
                }
            }
            catch {
                return;
            }
        }

        private void RecvCommunication(int clientNum)
        {
            /*
            // 受信処理.
            try
            {
                while (m_socket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[BUFFER_SIZE];

                    int recvSize = m_socket.Receive(buffer, buffer.Length, SocketFlags.None);
                    if (recvSize == 0)
                    {
                        // 切断.
                        Debug.Log("Disconnect recv from client.");
                        Disconnect();
                    }
                    else if (recvSize > 0)
                    {
                        m_recvQueue.Enqueue(buffer, recvSize);
                    }
                }
            }
            catch
            {
                return;
            }*/
        }

        /// <summary>
        /// 送信処理
        /// </summary>
        /// <param name="data"></param>
        /// <param name="size"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public int Send(string message, int id)
        {
            var data = _encoding.GetBytes(message + "\n");
            return Send(data, data.Length, id);
        }

        /// <summary>
        /// 送信処理
        /// </summary>
        /// <param name="data"></param>
        /// <param name="size"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public int Send(byte[] data, int size, int id)
        {
            if (id >= _clients.Count || _clients[id].Connected == false || _clients[id].sendQueue == null)
            {
                return 0;
            }

            return _clients[id].sendQueue.Enqueue(data, size);
        }

        /// <summary>
        /// 受信処理
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="size"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        public int Receive(ref byte[] buffer, int size, int id)
        {
            if (id < _clients.Count && _clients[id].Connected == false || _clients[id].recvQueue == null)
            {
                return 0;
            }

            return _clients[id].recvQueue.Dequeue(ref buffer, size);
        }

        /// <summary>
        /// 全てのクライアントとの接続を切断します
        /// </summary>
        public void DisConnectAll()
        {
            for (int i = 0; i < _clients.Count; i++)
            {
                DisConnect(_clients[i]);
            }
            _communicateLoop = false;
            _listenLoop = false;
        }

        /// <summary>
        /// クライアントとの接続を切断します
        /// </summary>
        /// <param name="data"></param>
        public void DisConnect(TcpClientData data)
        {
            NetEventState state = new NetEventState();
            state.type = NetEventType.Disconnect;

            lock (_lockObj)
            {
                if (!data.Connected || !data.client.Connected || !data.client.Client.Connected)
                {
                    state.result = NetEventResult.Failure;
                }
                else
                {
                    data.reader.Close();
                    data.client.Close();
                    data.Connected = false;

                    state.result = NetEventResult.Success;
                }
            }

            // 切断を通知します.
            _handler?.Invoke(state);
        }

        /// <summary>
        /// サーバを停止し、すべてのクライアントとの接続を切る
        /// </summary>
        public void Close()
        {
            _listenLoop = false;
            _communicateLoop = false;

            DisConnectAll();

            _listener.Stop();

#if SERVER_LOG
            UnityEngine.Debug.Log("[Server] Server stopped.");
#endif
        }


        /// <summary>
        /// イベント通知関数登録
        /// </summary>
        /// <param name="handler"></param>
        public void RegisterEventHandler(EventHandler handler)
        {
            _handler += handler;
        }

        /// <summary>
        /// イベント通知関数削除
        /// </summary>
        /// <param name="handler"></param>
        public void UnregisterEventHandler(EventHandler handler)
        {
            _handler -= handler;
        }

    }
}